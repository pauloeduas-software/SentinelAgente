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

- **WebSocket puro** (não SignalR) em `ws://` ou `wss://` `<servidor>/agent-hub`. Em desenvolvimento, a API
  do SentinelWeb fica na porta 3001.
- **`Authorization: Bearer <token da máquina>`** na abertura. Sem ele, ou com um token que o servidor não
  emitiu ou revogou, a resposta é 401 antes do upgrade e a conexão nem chega a existir. O segredo de
  instalação não abre o hub.
- **A conexão fica presa ao HWID do token:** o `Hwid` de qualquer mensagem que não é o do token fecha a
  conexão com 1008.
- **Uma conexão por HWID.** A conexão nova da mesma máquina substitui a velha, e o fechamento da velha não
  derruba a máquina.
- **Do lado do agente** ([A11](../decisoes/protocolo.md#a11--a-conexão-endereço-na-configuração-wss-e-um-token-por-máquina)):
  o endereço é o `Server:Url` de `/etc/sentinel-agent/agent.json`, só `wss://` (`ws://` em `localhost`). O
  segredo de instalação fica em `Server:EnrollSecretFile` até o cadastro, e o token, em `Server:TokenFile`
  (`/var/lib/sentinel-agent/token`), os dois com `chmod 600`. Quem faz isso é o `Connection/AgentConnection.cs`.

## O cadastro

Cada máquina tem o seu token, e ele nasce aqui, pelo mesmo endereço do hub (`wss://` vira `https://`):

```
POST /agent-hub/enroll
Authorization: Bearer <AGENT_ENROLL_SECRET>
{ "Hwid": "…" }
```

| Resposta | Quando |
|---|---|
| `201 { "Token": "…" }` | o token novo: 64 caracteres hexadecimais. O servidor guarda só o hash, e o token anterior da máquina deixa de valer |
| `401` | o segredo de instalação não confere |
| `409` | a máquina está conectada: o recadastro não toma o lugar dela |
| `422` | o corpo sem `Hwid` |

O teto é de 20 cadastros por minuto por IP. Revogar é excluir a máquina em Agentes, no SentinelWeb: o
token deixa de valer e a conexão aberta fecha com 1008. Sem `AGENT_ENROLL_SECRET` configurado, o servidor não
sobe em produção, e fora dela aceita qualquer cadastro.

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
([A25](../decisoes/protocolo.md#a25--o-handshake-leva-a-versão-do-agente-e-vai-de-novo-uma-vez-por-dia)). É nele que o servidor liga aquela conexão ao HWID: sem ele, a
telemetria até é gravada, mas o comando não chega, e a tela responde que o agente não está conectado (409).
É também ele que cria a máquina.

| Campo | O servidor | Onde o Linux guarda |
|---|---|---|
| `Hwid` | a chave da conexão (`endpoints.hwid`, único). O HWID nunca visto herda a linha de uma única máquina desligada com a mesma série. Quando os dois lados têm UUID, o UUID também tem de bater. Quando só a série veio, ou a linha antiga não tem UUID, a série basta | um UUID sorteado na instalação e guardado em `/var/lib/sentinel-agent/hwid` (`Identity/Hwid.cs`, [A20](../decisoes/protocolo.md#a20--o-hwid-é-o-id-da-instalação-e-quem-reconhece-a-máquina-é-o-sentinelweb-como-no-glpi)) |
| `Hostname` | o nome da máquina na tela; só vira dica de vínculo no SentinelWeb, nunca vínculo. Ausente vira "desconhecido" | `/proc/sys/kernel/hostname`, sem o domínio |
| `AgentVersion` | a lista de Agentes e a aba Máquina mostram (`endpoints.agentVersion`). Ausente apaga a gravada: é o agente anterior à A25 | a `<Version>` do csproj, sem o `+commit` do SDK (`Inventory/AgentVersion.cs`); vai sempre |
| `OsVersion` | a aba Máquina mostra. Ausente vira "desconhecido" | `/etc/os-release`, `NAME VERSION_ID` (sem `NAME`, "Linux"; versão que é data de snapshot não vai); vai sempre |
| `CpuModel` | a aba Máquina mostra | `/proc/cpuinfo` (`model name`); no ARM, não vai |
| `MacAddress` | a aba Máquina mostra | `/sys/class/net/<placa>/address` da placa da rota padrão de menor métrica, com o túnel de VPN para trás ([A15](../decisoes/protocolo.md#a15--a-revisão-da-identidade-e-do-inventário-o-lixo-sem-separadores-a-lenovo-sempre-o-túnel-pelo-tipo)) |
| `LocalIp` | a aba Máquina mostra | o IPv4 dessa placa na rede do gateway |
| `BiosSerial` | a aba Máquina mostra; a série única liga a máquina ao computador sozinha; com o `SystemUuid`, reconhece a máquina reinstalada | `/sys/class/dmi/id/product_serial`, e o `board_serial` quando ele é de fábrica (só root); os dois de fábrica, não vai |
| `SystemUuid` | não entra no vínculo; junto com a série, reconhece a máquina reinstalada | `/sys/class/dmi/id/product_uuid` (só root); de fábrica, não vai |
| `Manufacturer` | a aba Máquina mostra | `/sys/class/dmi/id/sys_vendor`; de fábrica, não vai |
| `HardwareModel` | a aba Máquina mostra. **Nunca** `Model` | `/sys/class/dmi/id/product_name`; na Lenovo, sempre o `product_version` |
| `Gpus` | guarda em `endpoints.gpus` cada placa como `{ Name, MemoryBytes }`, sem a que não tem `Name` e até 16 placas, e a aba Máquina mostra o nome e a memória | lista de `{ Name, MemoryBytes }`: os dispositivos de classe `0x03` do `/sys/bus/pci/devices`, com o nome do `pci.ids` e a VRAM do `amdgpu` (`Inventory/GpuInfo.cs`, [A22](../decisoes/protocolo.md#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar), [A24](../decisoes/protocolo.md#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem)) |

- **Valor de fábrica:** o campo não vem, e o servidor o lê como ausente.
- **`MacAddress`, `LocalIp` e `CpuModel` ausentes apagam o valor gravado;** `BiosSerial`, `SystemUuid`,
  `Manufacturer`, `HardwareModel` e `Gpus`, não: o que não veio fica como estava.
- **O servidor não compara mais um handshake com o anterior:** a mudança de hardware (`AssetChange`) saiu, com
  a memória total.
- **Do lado do agente** ([A14](../decisoes/protocolo.md#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera)):
  quem monta é o `Inventory/MachineInventory.cs`, relido a cada conexão e a cada reenvio diário, na forma que só
  muda quando a máquina muda.
- **O que o servidor não lê, ele ignora sem erro**, e o agente não manda: nem o `RamTotalBytes` nem os
  programas instalados, que o servidor tirou do parser ([A24](../decisoes/protocolo.md#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem)), nem o usuário logado
  ([A8](../decisoes/escopo.md#a8--o-handshake-não-manda-programas-instalados-nem-usuário-logado)).

---

## Telemetry

| Campo | Unidade |
|---|---|
| `CpuUsagePercentage` | porcentagem, de 0 a 100 |
| `RamTotalBytes`, `RamUsedBytes` | bytes |
| `Disks` | lista de `{ Name, TotalGb, UsedGb }`, com GB = 1024³ bytes; o disco sem `Name` sai, e a lista para em 64 |

- **Chega quando a conexão sobe, de hora em hora e em resposta ao `REFRESH`.**
- **Uma amostra é uma linha** em `telemetries`, com a hora em que **chegou** ao servidor: o protocolo não tem
  campo de hora, e o agente não guarda a amostra que não pôde mandar
  ([A17](../decisoes/escopo.md#a17--sem-buffer-offline-a-amostra-sem-conexão-é-descartada)). Só a última
  amostra de cada máquina fica: o expurgo diário apaga as outras.
- **O intervalo é do agente.** O servidor não impõe nenhum. Este agente manda de hora em hora
  ([A12](../decisoes/protocolo.md#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s)).
- Campo ausente vira zero, e o `Disks` ausente vira lista vazia.
- A telemetria de HWID que nunca mandou handshake é descartada.
- **Rede e processos o servidor não lê**, e o agente não manda ([A24](../decisoes/protocolo.md#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem)).
- **Do lado do agente**
  ([A16](../decisoes/protocolo.md#a16--a-telemetria-a-média-desde-a-amostra-anterior-o-disco-como-o-df-a-rede-só-pela-placa-física)):
  o `Telemetry/TelemetryService.cs` manda a primeira amostra quando a conexão sobe e depois uma por hora; sem
  conexão, ela é descartada. Cada campo sai de um coletor:

  | Campo | Coletor | O que vai |
  |---|---|---|
  | `CpuUsagePercentage` | `Telemetry/Cpu/CpuUsage.cs` | a média desde a amostra anterior, pelo `/proc/stat` (a primeira, de 10 s) |
  | `RamTotalBytes`, `RamUsedBytes` | `Telemetry/Memory/MemoryUsage.cs` | o `MemTotal` cru e `MemTotal − MemAvailable`, do `/proc/meminfo` |
  | `Disks` | `Telemetry/Disk/DiskUsage.cs` | cada dispositivo local do `/proc/mounts`, uma vez, sem `tmpfs`, `squashfs`, `overlay` e afins; total e usado como o `df` |

- **O contêiner (LXC, Docker) não é para o agente:** o serial que ele mandaria é o do hospedeiro (A20).
- **No ZFS, cada dataset é um disco** com o espaço livre do pool inteiro, como o Telegraf e o `df` mostram:
  somar os totais conta o pool mais de uma vez. O servidor não soma.
- **O envio que não chegou não para nada:** a conexão que cai no meio do envio devolve `false` ao
  `IServerLink.TrySendAsync`, e a amostra é descartada como sem conexão.

- **O uso da GPU não vai.** As placas de vídeo vão no handshake, como inventário ([A22](../decisoes/protocolo.md#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar)).

## Ping

Só prova que a máquina está viva. Qualquer mensagem faz o mesmo, mas a telemetria é de hora em hora, e é o
`Ping` que mantém a máquina como Conectado (`ONLINE`) entre uma amostra e outra. A máquina vira Desconectado
(`OFFLINE`) na hora em que a conexão fecha, ou depois de **2 minutos** sem mensagem nenhuma; o servidor
confere a cada 60 s.

- **Do lado do agente**
  ([A12](../decisoes/protocolo.md#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s)): o `Ping`
  vai quando a conexão fica calada por um tempo sorteado entre 30 e 60 s (`Connection/ConnectionTimings.cs`).

---

## Command (do servidor para o agente)

```json
{ "Type": "Command", "Payload": { "Action": "SHUTDOWN", "CommandId": "6f1c…" } }
```

- `Action` é `SHUTDOWN`, `REBOOT` ou `SUSPEND`, sempre em maiúsculas. O servidor só aceita esses três da tela,
  e só de quem é **Supervisor** ou acima.
- `CommandId` é um UUID novo por comando, gravado na auditoria do servidor: é a chave para cruzar com o log
  do agente.
- **Não há fila.** Comando para máquina desconectada é recusado na hora (409), nunca executado depois.
- **502** quando a conexão cai antes de o pacote sair: nada foi entregue.
- **Não há resposta.** A API responde "Comando enviado." quando a conexão aceitou os bytes, e a tela mostra
  "Comando de … entregue ao agente de …"; o agente não confirma, e mensagem de confirmação seria descartada como tipo desconhecido.
- **Do lado do agente**
  ([A5](../decisoes/comandos.md#a5--comando-é-lista-fechada-sem-shell),
  [A18](../decisoes/comandos.md#a18--o-comando-roda-o-systemctl-direto-respeita-os-bloqueios-e-diz-no-log-como-terminou)):
  o `Commands/PowerCommands.cs` troca `SHUTDOWN`, `REBOOT` e `SUSPEND` por `/usr/bin/systemctl` `poweroff`,
  `reboot` e `suspend`, sem shell, e recusa qualquer outra `Action`. Ele dispara e volta; o log diz, com o
  `CommandId`, se o systemd aceitou ou o motivo da recusa (bloqueio do logind, máquina que não suspende).

## O pedido de dados agora (`REFRESH`)

```json
{ "Type": "Command", "Payload": { "Action": "REFRESH", "CommandId": "9a2e…" } }
```

- O servidor manda pelo Atualizar agora da ficha e da lista de Agentes, de quem é **Técnico** ou acima,
  e só ao agente 0.2.0 ou mais novo.
- **409** para a máquina desconectada e para o agente anterior à 0.2.0 ou sem versão: o pedido não sai.
  **502** quando o envio falha.
- **Há resposta:** o agente manda o `Handshake`, com o inventário relido, e uma `Telemetry` na hora. O servidor
  espera as duas gravadas, e a tela mostra "Máquina atualizada."; sem as duas em 15 s, é **504**.
- **Do lado do agente** ([A26](../decisoes/protocolo.md#a26--o-refresh-manda-o-inventário-e-a-telemetria-na-hora-a-pedido-do-painel)): a conexão trata o pedido, e ele não chega ao
  `PowerCommands`. O `IServerLink.RefreshRequested` avisa a telemetria, que manda a amostra sem mexer no relógio
  da hora.

## Quando a conexão fecha

- **O servidor encerrando** fecha todas as conexões com o código 1001 ("Servidor encerrando"): o agente
  reconecta.
- **O agente encerrando** leva a máquina a OFFLINE na hora, a não ser que uma conexão mais nova da mesma
  máquina já a tenha substituído.
- **O HWID da mensagem diferente do token** o servidor fecha com o código 1008 ("HWID diferente do token"):
  o arquivo do HWID foi apagado ou trocado depois do cadastro (A20), ou o token veio de outra instalação.
- **A máquina excluída em Agentes** o servidor fecha com 1008 ("Máquina excluída"), e a
  reconexão leva 401. Nos dois 1008, o agente põe o motivo do servidor no log, espera o teto e não se recadastra
  sozinho; o recadastro é pôr o segredo de instalação de novo.
- **A mensagem acima de 1 MiB** o servidor fecha com 1009.
- **Do lado do agente** (`Connection/AgentConnection.cs`): a conexão que cai antes de 1 minuto conta como falha,
  e a espera seguinte dobra. O erro que não se conserta tentando de novo (a máquina sem HWID, o `CaFile`
  ilegível) para o serviço com código de saída 1, e o systemd vê a falha.
- **Com o segredo de instalação e o token no disco**, o cadastro que não dá certo (409, rede) não impede a
  conexão: o agente conecta com o token gravado, e o servidor diz se ele ainda vale.
