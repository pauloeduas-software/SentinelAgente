# O protocolo com o SentinelWeb

> O que o agente manda e recebe pelo `/agent-hub`, e o que o servidor faz com isso hoje. **A fonte é o
> SentinelWeb,** na referência de lá
> ([`docs/referencia/agente.md`](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/referencia/agente.md)):
> se as duas divergirem, vale o servidor e esta página está errada. O porquê das regras do servidor mora lá;
> aqui fica o fato, e o porquê do lado do agente, nas decisões daqui. Os testes do servidor
> ([`tests/helpers/agente.ts`](https://github.com/pauloeduas-software/SentinelWeb/blob/main/tests/helpers/agente.ts)) falam este protocolo e são o exemplo que
> funciona.

---

## A conexão

- **WebSocket puro** (não SignalR) em `ws://` ou `wss://` `<servidor>/agent-hub`. O SentinelWeb escuta HTTP
  puro: o `wss://` passa por um proxy com TLS na frente dele. Em desenvolvimento, a API fica na porta 3001.
- **`Authorization: Bearer <token da máquina>`** na abertura. Sem ele, ou com um token que o servidor não
  emitiu ou revogou, a resposta é 401 antes do upgrade e a conexão nem chega a existir. O segredo de
  instalação não abre o hub.
- **A conexão fica presa ao HWID do token:** o `Hwid` de uma mensagem reconhecida que não é o do token fecha
  a conexão com 1008.
- **Uma conexão por HWID para os comandos.** O handshake da conexão nova da mesma máquina toma o lugar da
  velha; o servidor não fecha a velha, e o fechamento dela não derruba a máquina.
- **O teto de 300 requisições por minuto por IP** vale também para a abertura do hub: acima dele, 429 antes
  do upgrade. O IP é o que chega no socket, sem o `X-Forwarded-For`: as máquinas atrás do mesmo NAT, ou do
  proxy na frente do servidor, dividem este teto e o do cadastro.
- **Do lado do agente** ([A11](../decisoes/protocolo.md#a11--a-conexão-endereço-na-configuração-wss-e-um-token-por-máquina)):
  o endereço é o `Server:Url` de `/etc/sentinel-agent/agent.json`, só `wss://` (`ws://` em `localhost`). O
  segredo de instalação fica em `Server:EnrollSecretFile` (`/etc/sentinel-agent/enroll-secret`) até o
  cadastro, e o token, em `Server:TokenFile` (`/var/lib/sentinel-agent/token`), os dois com `chmod 600`: o
  agente recusa o arquivo com permissão para outro usuário. Servidor com certificado interno entra pelo
  `Server:CaFile`, um PEM com a CA que passa a valer sozinha. A configuração é conferida na subida pelo
  `Config/ServerOptions.cs`, os arquivos de segredo são lidos e gravados pelo `Config/SecretFile.cs`, a CA
  própria é do `Connection/ServerCertificate.cs`, e quem conecta é o `Connection/AgentConnection.cs`.

## O cadastro

Cada máquina tem o seu token, e ele nasce aqui, pelo mesmo endereço do hub (`wss://` vira `https://`, e
`ws://`, `http://`):

```
POST /agent-hub/enroll
Authorization: Bearer <AGENT_ENROLL_SECRET>
{ "Hwid": "…" }
```

| Resposta | Quando |
|---|---|
| `201 { "Token": "…" }` | o token novo: 64 caracteres hexadecimais. O servidor guarda só o hash, e o token anterior da máquina deixa de valer |
| `401` | o segredo de instalação não confere |
| `409` | a máquina tem conexão aberta, com handshake: o recadastro não toma o lugar dela |
| `413` | o corpo acima de 8 KiB, antes de conferir o segredo |
| `422` | o corpo sem `Hwid`, ou com ele vazio |
| `429` | mais de 20 cadastros no minuto, do mesmo IP |

Revogar é excluir a máquina em Agentes, no SentinelWeb, por quem é **Supervisor** ou acima, e só a que não
está vinculada a um computador: o token deixa de valer, a linha e a telemetria saem, e a conexão aberta fecha
com 1008. Sem `AGENT_ENROLL_SECRET` configurado, o servidor não sobe em produção, e fora dela aceita qualquer
cadastro.

**Do lado do agente** (`Connection/Enrollment.cs`): o cadastro tem 30 s para responder, e o `201` sem token
conta como falha. Depois do `201`, o agente grava o token e apaga o segredo.

---

## O envelope

```json
{ "Type": "Handshake", "Payload": { "Hwid": "…", "Hostname": "…" } }
```

- `Type` é `Handshake`, `Telemetry` ou `Ping`. Qualquer outro é descartado com um aviso no log
  do servidor.
- `Payload` é um objeto e leva `Hwid` em toda mensagem; sem ele, a mensagem é descartada. O servidor tira os
  espaços e passa o HWID para minúsculas.
- **Os nomes vão em PascalCase**, o envelope e o payload: é a única grafia que o servidor lê.
- **A mensagem tem teto de 1 MiB:** a maior fecha a conexão com 1009 antes de ser lida.

---

## Handshake

**Sempre a primeira mensagem de cada conexão, e de novo uma vez por dia com a conexão de pé**
([A25](../decisoes/protocolo.md#a25--o-handshake-leva-a-versão-do-agente-e-vai-de-novo-uma-vez-por-dia)). É nele que o servidor liga aquela conexão ao HWID:
sem ele nesta conexão, a telemetria de uma máquina já conhecida até é gravada, mas o comando não chega, e a tela responde que o agente não está conectado (409).
É também ele que cria a máquina.

| Campo | O servidor | Onde o Linux guarda |
|---|---|---|
| `Hwid` | a chave da conexão (`endpoints.hwid`, único). O HWID nunca visto herda a linha da máquina com a mesma série, se só uma servir e ela estiver desligada, e o token da instalação velha deixa de valer. Quando os dois lados têm UUID, o UUID também tem de bater. Quando só a série veio, ou a linha antiga não tem UUID, a série basta | um UUID sorteado no primeiro arranque e guardado em `/var/lib/sentinel-agent/hwid` (`Identity/Hwid.cs`, [A20](../decisoes/protocolo.md#a20--o-hwid-é-o-id-da-instalação-e-quem-reconhece-a-máquina-é-o-sentinelweb-como-no-glpi)) |
| `Hostname` | o nome da máquina na tela; só vira dica de vínculo no SentinelWeb, nunca vínculo. Ausente vira "desconhecido" | `/proc/sys/kernel/hostname`, sem o domínio |
| `AgentVersion` | a lista de Agentes e a tela mostram (`endpoints.agentVersion`). Ausente apaga a gravada: é o agente anterior à A25 | a `<Version>` do csproj, sem o `+commit` do SDK (`Inventory/AgentVersion.cs`); vai sempre |
| `OsVersion` | a tela mostra. Ausente vira "desconhecido" | `/etc/os-release` (ou `/usr/lib/os-release`), `NAME VERSION_ID` (sem `NAME`, "Linux"; versão que é data de snapshot não vai); vai sempre |
| `CpuModel` | a tela mostra | `/proc/cpuinfo` (`model name`); no ARM, não vai |
| `MacAddress` | a tela mostra | `/sys/class/net/<placa>/address` da placa da rota padrão de menor métrica, com o túnel de VPN para trás ([A15](../decisoes/protocolo.md#a15--a-revisão-da-identidade-e-do-inventário-o-lixo-sem-separadores-a-lenovo-sempre-o-túnel-pelo-tipo)) |
| `LocalIp` | a tela mostra | o IPv4 dessa placa na rede do gateway, senão o primeiro IPv4 dela |
| `BiosSerial` | a tela mostra; a série única liga a máquina ao computador sozinha; com o `SystemUuid`, reconhece a máquina reinstalada | `/sys/class/dmi/id/product_serial`, e o `board_serial` quando o do produto é de fábrica (só root); os dois de fábrica, não vai |
| `SystemUuid` | não entra no vínculo; junto com a série, reconhece a máquina reinstalada | `/sys/class/dmi/id/product_uuid` (só root); de fábrica, não vai |
| `Manufacturer` | a tela mostra | `/sys/class/dmi/id/sys_vendor`; de fábrica, não vai |
| `HardwareModel` | a tela mostra. **Nunca** `Model` | `/sys/class/dmi/id/product_name`; na Lenovo, o `product_version` quando ele não é de fábrica; de fábrica, não vai |
| `Gpus` | guarda em `endpoints.gpus` cada placa como `{ Name, MemoryBytes }`, sem a que não tem `Name` e até 16 placas, e a tela mostra o nome e a memória | lista de `{ Name, MemoryBytes }`: os dispositivos de classe `0x03` do `/sys/bus/pci/devices`, com o nome do `pci.ids`, o que está entre colchetes quando há (sem o arquivo, o par `fabricante:dispositivo`) e a memória da placa dedicada: a VRAM do `amdgpu` e o `/usr/bin/nvidia-smi` da NVIDIA, sem shell e com 10 s para responder; a integrada vai sem, menos a AMD sem o `hwmon` (`amdgpu.dpm=0`), que vai com a reserva da BIOS. Sem placa, vai a lista vazia, e o servidor apaga a gravada; sem o `/sys/bus/pci/devices`, o campo não vai, e a gravada fica (`Inventory/GpuInfo.cs`, [A22](../decisoes/protocolo.md#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar), [A28](../decisoes/protocolo.md#a28--a-memória-da-placa-de-vídeo-a-dedicada-vai-a-integrada-não), [A24](../decisoes/protocolo.md#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem)) |

- **A tela** é o card de cima da ficha da máquina, em Agentes, e a aba Máquina do computador e do posto: mostra
  o que o handshake traz, menos o `SystemUuid`, e o HWID com o botão de copiar. A lista de Agentes mostra o
  nome, a série, o IP, o sistema e a versão.
- **Valor de fábrica:** o campo não vem. Para o servidor, `null`, texto vazio ou só com espaço também são
  ausente. Ele confere de novo a série e o UUID com as mesmas listas de fábrica antes de ligar ou herdar, e a
  coluna guarda o valor como veio (`normalize-identity.helper.ts`).
- **`MacAddress`, `LocalIp` e `CpuModel` ausentes apagam o valor gravado;** `BiosSerial`, `SystemUuid`,
  `Manufacturer`, `HardwareModel` e `Gpus`, não: o que não veio fica como estava. O `Gpus: []` apaga a lista, e
  o `MemoryBytes` que não é número maior que zero vira `null`.
- **O servidor não compara um handshake com o anterior:** não registra mudança de hardware.
- **No contêiner (LXC, Docker)**, o `/sys/class/dmi/id` é o do hospedeiro: o `BiosSerial` e o `SystemUuid` que
  iriam são os dele.
- **Do lado do agente** ([A14](../decisoes/protocolo.md#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera),
  [A25](../decisoes/protocolo.md#a25--o-handshake-leva-a-versão-do-agente-e-vai-de-novo-uma-vez-por-dia), [A26](../decisoes/protocolo.md#a26--o-refresh-manda-o-inventário-e-a-telemetria-na-hora-a-pedido-do-painel)):
  quem monta é o `Inventory/MachineInventory.cs`, relido a cada conexão, a cada `REFRESH` e a cada 24 horas
  com a conexão de pé, contadas desde que ela subiu, na forma que só muda quando a máquina muda.
- **O que o servidor não lê, ele ignora sem erro**, e o agente não manda: nem o `RamTotalBytes` nem os
  programas instalados, que o servidor não lê ([A24](../decisoes/protocolo.md#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem)), nem o usuário logado
  ([A8](../decisoes/escopo.md#a8--o-handshake-não-manda-programas-instalados-nem-usuário-logado)).

---

## Telemetry

| Campo | Unidade |
|---|---|
| `CpuUsagePercentage` | porcentagem, de 0 a 100 |
| `RamTotalBytes`, `RamUsedBytes` | bytes |
| `Disks` | lista de `{ Name, TotalGb, UsedGb }`, com GB = 1024³ bytes; o disco sem `Name` sai, e a lista para em 64 |

- **Chega 10 s depois da primeira conexão desde que o serviço subiu (no boot e a cada reinício), de hora em
  hora e em resposta ao `REFRESH`.** A reconexão do agente que segue de pé não manda amostra: a próxima vem
  na hora seguinte.
- **Uma amostra é uma linha** em `telemetries`, com a hora em que **chegou** ao servidor: o protocolo não tem
  campo de hora, e o agente não guarda a amostra que não pôde mandar
  ([A17](../decisoes/escopo.md#a17--sem-buffer-offline-a-amostra-sem-conexão-é-descartada)). Só a última
  amostra de cada máquina fica: o expurgo diário apaga as outras.
- **O intervalo é do agente.** O servidor não impõe nenhum. Este agente manda de hora em hora
  ([A12](../decisoes/protocolo.md#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s)).
- Campo ausente vira zero, e o `Disks` ausente vira lista vazia.
- A telemetria de HWID que nunca mandou handshake é descartada. O servidor trata cada mensagem quando chega,
  sem esperar a anterior: a telemetria colada no primeiro handshake de uma máquina nova pode chegar antes de a
  linha existir, e é descartada.
- A última amostra aparece na aba Máquina da ficha da máquina, em Agentes, e na do computador e do posto.
- **Rede e processos o servidor não lê**, e o agente não manda ([A24](../decisoes/protocolo.md#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem)).
- **Do lado do agente**
  ([A16](../decisoes/protocolo.md#a16--a-telemetria-a-média-desde-a-amostra-anterior-o-disco-como-o-df-a-rede-só-pela-placa-física)):
  o `Telemetry/TelemetryService.cs` segue o relógio de cima, caia a conexão ou não; sem conexão, a amostra é
  descartada. Cada campo sai de um coletor:

  | Campo | Coletor | O que vai |
  |---|---|---|
  | `CpuUsagePercentage` | `Telemetry/Cpu/CpuUsage.cs` | a média desde a amostra anterior, pelo `/proc/stat` (a primeira, de 10 s) |
  | `RamTotalBytes`, `RamUsedBytes` | `Telemetry/Memory/MemoryUsage.cs` | o `MemTotal` cru e `MemTotal − MemAvailable`, do `/proc/meminfo` |
  | `Disks` | `Telemetry/Disk/DiskUsage.cs` | cada dispositivo local (`/dev/…` ou dataset do ZFS) do `/proc/mounts`, uma vez, no primeiro ponto de montagem, que vira o `Name`; sem `tmpfs`, `devtmpfs`, `devfs`, `iso9660`, `overlay`, `aufs` e `squashfs`; total e usado como o `df`, em GB com duas casas |

  O coletor que não lê manda a parte dele zerada (o `Disks`, vazio), e a exceção, quando há, vai para o log:
  a telemetria leva sempre todos os campos.

- **No ZFS, cada dataset é um disco** com o espaço livre do pool inteiro, como o Telegraf e o `df` mostram:
  somar os totais conta o pool mais de uma vez. O servidor não soma.
- **O envio que não chegou não para nada:** a conexão que cai no meio do envio devolve `false` ao
  `IServerLink.TrySendAsync`, e a amostra é descartada como sem conexão.

- **O uso da GPU não vai.** As placas de vídeo vão no handshake, como inventário ([A22](../decisoes/protocolo.md#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar)).

## Ping

Só prova que a máquina está viva. Qualquer mensagem faz o mesmo e grava o Último contato
(`endpoints.lastSeen`), mas a telemetria é de hora em hora, e é o `Ping` que mantém a máquina como Conectado
(`ONLINE`) entre uma amostra e outra. A máquina vira Desconectado (`OFFLINE`) na hora em que a conexão fecha,
com qualquer código (menos no servidor encerrando, abaixo), ou depois de **2 minutos** sem mensagem nenhuma;
o servidor confere a cada 60 s. Ele não manda ping do WebSocket nem fecha a conexão calada: o silêncio só muda
o status.

- **Do lado do agente**
  ([A12](../decisoes/protocolo.md#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s)): o `Ping`
  vai quando o agente passa um tempo sorteado entre 30 e 60 s sem mandar mensagem
  (`Connection/ConnectionTimings.cs`).
  Fora ele, o agente manda o ping do próprio WebSocket a cada 30 s e derruba a conexão sem a resposta em 15 s,
  o que pega a conexão morta depois da suspensão; esse ping não conta para o ONLINE.

---

## Command (do servidor para o agente)

```json
{ "Type": "Command", "Payload": { "Action": "SHUTDOWN", "CommandId": "6f1c…" } }
```

- `Action` é `SHUTDOWN`, `REBOOT` ou `SUSPEND`, ou o `REFRESH` (abaixo), sempre em maiúsculas. Os três de
  energia o servidor só aceita da tela, de quem é **Supervisor** ou acima.
- `CommandId` é um UUID novo por comando, gravado na auditoria do servidor: é a chave para cruzar com o log
  do agente.
- **Não há fila.** Comando para máquina desconectada é recusado na hora (409), nunca executado depois.
- **502** quando a conexão cai antes de o pacote sair: nada foi entregue.
- **Não há resposta.** A API responde "Comando enviado.", com o `commandId`, quando a conexão aceitou os bytes, e a tela mostra
  "Comando de … entregue ao agente de …"; o agente não confirma, e mensagem de confirmação seria descartada
  como tipo desconhecido.
- **Do lado do agente**
  ([A5](../decisoes/comandos.md#a5--comando-é-lista-fechada-sem-shell),
  [A18](../decisoes/comandos.md#a18--o-comando-roda-o-systemctl-direto-respeita-os-bloqueios-e-diz-no-log-como-terminou)):
  o `Commands/PowerCommands.cs` troca `SHUTDOWN`, `REBOOT` e `SUSPEND` por `/usr/bin/systemctl` `poweroff`,
  `reboot` e `suspend`, sem shell, e recusa qualquer outra `Action`. Ele dispara e volta; o log diz, com o
  `CommandId`, se o systemd aceitou, o motivo da recusa (bloqueio do logind, máquina que não suspende) ou que
  o `systemctl` não voltou em 15 s e foi interrompido.
- **O que chega fora do protocolo** o agente descarta com aviso no log: o tipo que não é `Command`, o `Command`
  sem `Action` ou sem `CommandId` e o que não é JSON. A mensagem acima de 64 KiB derruba a conexão.

## O pedido de dados agora (`REFRESH`)

```json
{ "Type": "Command", "Payload": { "Action": "REFRESH", "CommandId": "9a2e…" } }
```

- O servidor manda pelo Atualizar agora da ficha e da lista de Agentes, de quem é **Técnico** ou acima,
  e só ao agente 0.2.0 ou mais novo, pelo `X.Y.Z` do começo do `AgentVersion`.
- **409** para a máquina desconectada e para o agente anterior à 0.2.0 ou sem versão: o pedido não sai.
  **502** quando o envio falha.
- **Há resposta:** o agente manda o `Handshake`, com o inventário relido, e uma `Telemetry` na hora. O servidor
  espera as duas gravadas, e a tela mostra "Máquina atualizada."; sem as duas em 15 s, é **504**. O servidor
  não casa a resposta com o `CommandId`: qualquer `Handshake` e `Telemetry` da máquina nesses 15 s contam, e o
  segundo pedido na mesma máquina espera o primeiro, sem mandar outro `REFRESH`.
- **Do lado do agente** ([A26](../decisoes/protocolo.md#a26--o-refresh-manda-o-inventário-e-a-telemetria-na-hora-a-pedido-do-painel)):
  a conexão trata o pedido, e ele não chega ao `PowerCommands`. O `IServerLink.RefreshRequested` avisa a telemetria, que manda a amostra sem mexer no relógio
  da hora.

## Quando a conexão fecha

- **O servidor encerrando** fecha com 1001 ("Servidor encerrando") as conexões que mandaram handshake, e as
  outras sem código. A máquina não vira OFFLINE aí: a que não volta vira Desconectado na primeira conferência
  do servidor, de minuto em minuto, em que a última mensagem dela passa de 2 minutos. O agente reconecta.
- **O agente encerrando** (`systemctl stop`) fecha com 1000 ("Agente encerrando"), com até 5 s para isso, e
  leva a máquina a OFFLINE na hora, a não ser que uma conexão mais nova da mesma
  máquina já a tenha substituído.
- **O HWID da mensagem diferente do token** o servidor fecha com o código 1008 ("HWID diferente do token"):
  o arquivo do HWID foi apagado ou trocado depois do cadastro (A20), ou o token veio de outra instalação.
- **A máquina excluída em Agentes** o servidor fecha com 1008 ("Máquina excluída"), e a
  reconexão leva 401. **Outra instalação que herda a linha** (no `Hwid`, acima) também faz o token da velha
  deixar de valer: a reconexão dela leva 401. Nos dois 1008 e no 401 da abertura, o agente põe o motivo no log, espera o teto (5 min) e
  tenta de novo, relendo o token; não se recadastra sozinho, e o recadastro é pôr o segredo de instalação de
  novo. O cadastro recusado (401 do segredo, 409) sem token no disco espera o mesmo teto.
- **A mensagem acima de 1 MiB** o servidor fecha com 1009.
- **Do lado do agente** (`Connection/AgentConnection.cs`): a espera entre tentativas começa em 5 s e dobra a
  cada falha, até 5 min, sorteada entre a metade e o valor cheio (`Connection/Backoff.cs`). A conexão que fica
  1 minuto de pé zera a conta, e a que cai antes conta como falha. O erro que não se conserta tentando de novo
  (o arquivo do HWID que não abre ou foi mexido à mão, o `CaFile` ilegível) faz o agente sair com código 1, e o systemd o sobe de novo a cada
  30 s (`Restart=on-failure`), com o motivo no journal.
- **Com o segredo de instalação e o token no disco**, o cadastro que não dá certo (401, 409, rede, segredo
  ilegível) não impede a conexão: o agente conecta com o token gravado, e o servidor diz se ele ainda vale.
