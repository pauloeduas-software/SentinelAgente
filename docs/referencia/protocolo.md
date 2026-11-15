# O protocolo com o SentinelWeb

> O que o agente manda e recebe pelo `/agent-hub`. **A fonte é o SentinelWeb:** cada seção aponta o arquivo
> de lá, e se os dois divergirem, vale o servidor e esta página está errada. O agente falso do servidor
> ([`scripts/agente-falso.ts`](https://github.com/pauloeduas-software/SentinelWeb/blob/main/scripts/agente-falso.ts))
> fala este protocolo e é o exemplo que funciona.

---

## A conexão

- **WebSocket puro** (não SignalR) em `ws://` ou `wss://` `<servidor>/agent-hub`. Em desenvolvimento, a API
  do SentinelWeb fica na porta 3001.
- **`Authorization: Bearer <token da máquina>`** na abertura. Sem ele, ou com um token que o servidor não
  emitiu ou revogou, a resposta é 401 antes do upgrade e a conexão nem chega a existir
  (`server/modules/agent/agent.maestro.ts`). O segredo de instalação não abre o hub.
- **A conexão fica presa ao HWID do token:** mensagem com outro `Hwid` no payload é descartada.
- **Uma conexão por HWID.** A conexão nova da mesma máquina substitui a velha, e o fechamento da velha não
  derruba a máquina (`server/modules/agent/agent.registry.ts`).
- **Do lado do agente** ([A11](../decisoes/protocolo.md#a11--a-conexão-endereço-na-configuração-wss-e-token-que-só-o-root-lê)):
  o endereço é o `Server:Url` de `/etc/sentinel-agent/agent.json`, só `wss://` (`ws://` em `localhost`). O
  segredo de instalação fica em `Server:EnrollSecretFile` até o cadastro, e o token, em `Server:TokenFile`
  (`/var/lib/sentinel-agent/token`), os dois com `chmod 600`. Quem faz isso é o `Connection/AgentConnection.cs`.

## O cadastro

Cada máquina tem o seu token
([CR](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/acesso.md#cr--cada-máquina-tem-o-seu-token-e-o-segredo-único-só-cadastra)),
e ele nasce aqui, pelo mesmo endereço do hub (`wss://` vira `https://`):

```
POST /agent-hub/enroll
Authorization: Bearer <AGENT_ENROLL_SECRET>
{ "Hwid": "…" }
```

| Resposta | Quando |
|---|---|
| `201 { "Token": "…" }` | o token novo: 64 caracteres hexadecimais. O servidor guarda só o hash, e o token anterior da máquina deixa de valer |
| `401` | o segredo de instalação não confere |
| `409` | a máquina está conectada com outro token: o recadastro não toma o lugar dela |
| `422` | o corpo sem `Hwid` |

O teto é de 20 cadastros por minuto por IP. Revogar é do servidor (`bun run agente:revogar <hwid>`), e a
conexão já aberta só cai quando ele reinicia.

---

## O envelope

```json
{ "Type": "Handshake", "Payload": { "Hwid": "…", "Hostname": "…" } }
```

- `Type` é `Handshake`, `Telemetry` ou `Ping`. Qualquer outro é descartado com um aviso no log do servidor
  (`server/modules/agent/helpers/parse-agent-message.helper.ts`).
- `Payload` é um objeto e leva `Hwid` em toda mensagem; sem ele, a mensagem é descartada. O servidor tira os
  espaços e passa o HWID para minúsculas (`server/modules/endpoint/helpers/hwid.helper.ts`).
- Os nomes podem vir em PascalCase ou camelCase: o servidor lê os dois (`payload.helper.ts`). O agente falso
  manda PascalCase.

---

## Handshake

**Sempre a primeira mensagem de cada conexão.** É nele que o servidor liga aquela conexão ao HWID: sem ele, a
telemetria até é gravada, mas o comando não chega, e a tela responde que o agente não está conectado (409).
É também ele que cria a máquina: telemetria de HWID que nunca mandou handshake é descartada.

| Campo | O servidor | Onde o Linux guarda |
|---|---|---|
| `Hwid` | a chave da máquina (`endpoints.hwid`, único) | calculado em `Identity/Hwid.cs` ([A13](../decisoes/protocolo.md#a13--o-hwid-é-uma-fila-de-três-dados-do-firmware)) |
| `Hostname` | o nome da máquina na tela; sinal `HOSTNAME` da reconciliação. Ausente vira "desconhecido" | `/proc/sys/kernel/hostname`, sem o domínio |
| `OsVersion` | a aba Máquina mostra. Ausente vira "desconhecido" | `/etc/os-release`, `NAME VERSION_ID` (sem `NAME`, "Linux"; versão que é data de snapshot não vai); vai sempre |
| `CpuModel` | a aba Máquina mostra | `/proc/cpuinfo` (`model name`); no ARM, não vai |
| `MacAddress` | a aba Máquina mostra | `/sys/class/net/<placa>/address` da placa da rota padrão de menor métrica, com o túnel de VPN para trás ([A15](../decisoes/protocolo.md#a15--a-revisão-da-identidade-e-do-inventário-o-lixo-sem-separadores-a-lenovo-sempre-o-túnel-pelo-tipo)) |
| `LocalIp` | a aba Máquina mostra | o IPv4 dessa placa na rede do gateway |
| `BiosSerial` | sinal `SERIAL` da reconciliação, o de 100 pontos | `/sys/class/dmi/id/product_serial` (só root); de fábrica, não vai |
| `SystemUuid` | sinal `UUID` da reconciliação | `/sys/class/dmi/id/product_uuid` (só root); de fábrica, não vai |
| `Manufacturer` | grava | `/sys/class/dmi/id/sys_vendor`; de fábrica, não vai |
| `HardwareModel` | grava. **Nunca** `Model` | `/sys/class/dmi/id/product_name`; na Lenovo, sempre o `product_version` |
| `ChassisType` | grava | **o agente não manda**: o formato (código ou nome) não está fixado no servidor ([A14](../decisoes/protocolo.md#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera)) |
| `RamTotalBytes` | grava, em bytes | `/proc/meminfo` (`MemTotal`), arredondado para cima em GiB |
| `DiskTotalBytes` | grava, em bytes | **o agente não manda**: oscila com disco USB, e nenhuma tela mostra ([A14](../decisoes/protocolo.md#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera)) |

- **`MacAddress`, `LocalIp` e `CpuModel` ausentes apagam o valor gravado;** os campos de `BiosSerial` para
  baixo, não: o que não veio fica como estava (`register-handshake.usecase.ts`).
- **Os campos de `BiosSerial` para baixo, mais `CpuModel` e `OsVersion`, são comparados a cada handshake** de
  máquina vinculada a um computador, e a diferença vira uma linha de mudança de hardware (`AssetChange`). A
  comparação é exata: valor que oscila entre um handshake e outro — um pendrive somado no total de disco —
  vira mudança falsa.
- **Do lado do agente** ([A14](../decisoes/protocolo.md#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera)):
  quem monta é o `Inventory/MachineInventory.cs`, relido a cada conexão, na forma que só muda quando a máquina
  muda.
- `InstalledSoftware` e `LoggedOnUser` o servidor aceita e descarta. O usuário logado o agente não manda
  ([A8](../decisoes/escopo.md#a8--o-handshake-não-manda-programas-instalados-nem-usuário-logado)); os
  programas, só depois que o servidor passar a guardar
  ([A10](../decisoes/protocolo.md#a10--a-lista-de-programas-instalados-volta)).

---

## Telemetry

| Campo | Unidade |
|---|---|
| `CpuUsagePercentage` | porcentagem, de 0 a 100 |
| `RamTotalBytes`, `RamUsedBytes` | bytes |
| `Disks` | lista de `{ Name, TotalGb, UsedGb }`, com GB = 1024³ bytes |
| `Network` | `{ BytesReceived, BytesSent, RxSpeedKbps, TxSpeedKbps }`: os contadores em bytes, a velocidade em kbit/s (1 kbit = 1000 bits) |
| `TopProcesses` | lista de `{ Pid, Name, RamMb }` |

- **Uma amostra é uma linha** em `telemetries`, com a hora em que **chegou** ao servidor: o protocolo não tem
  campo de hora, e por isso o buffer offline espera o servidor
  ([A9](../decisoes/protocolo.md#a9--o-buffer-offline-volta-com-a-hora-da-coleta)). A amostra crua fica 3
  dias; o uso agregado por dia, para sempre.
- **O intervalo é do agente.** O servidor não impõe nenhum; o agente falso manda a cada 3 segundos, o que dá
  28.800 linhas por máquina por dia. Este agente vai mandar de hora em hora
  ([A12](../decisoes/protocolo.md#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s)).
- Campo ausente vira zero ou lista vazia (`normalize-payload.helper.ts`).
- **A tela mostra o total de rede pelos bytes.** `TotalRxGb` e `TotalTxGb` ela só lê quando os bytes não
  vêm (`src/helpers/metrics.helper.ts`).
- **Do lado do agente**
  ([A16](../decisoes/protocolo.md#a16--a-telemetria-a-média-desde-a-amostra-anterior-o-disco-como-o-df-a-rede-só-pela-placa-física)):
  o `Telemetry/TelemetryService.cs` manda a primeira amostra quando a conexão sobe e depois uma por hora; sem
  conexão, ela é descartada. Cada campo sai de um coletor:

  | Campo | Coletor | O que vai |
  |---|---|---|
  | `CpuUsagePercentage` | `Telemetry/Cpu/CpuUsage.cs` | a média desde a amostra anterior, pelo `/proc/stat` (a primeira, de 10 s) |
  | `RamTotalBytes`, `RamUsedBytes` | `Telemetry/Memory/MemoryUsage.cs` | o `MemTotal` cru e `MemTotal − MemAvailable`, do `/proc/meminfo` |
  | `Disks` | `Telemetry/Disk/DiskUsage.cs` | cada dispositivo local do `/proc/mounts`, uma vez, sem `tmpfs`, `squashfs`, `overlay` e afins; total e usado como o `df` |
  | `Network` | `Telemetry/Network/NetworkUsage.cs` | a soma das placas com `device/` no `/proc/net/dev`, e a velocidade média desde a amostra anterior |
  | `TopProcesses` | `Telemetry/Processes/TopProcesses.cs` | os 10 de maior `VmRSS`, um por PID, com o nome do `comm` |

- **GPU o servidor não recebe hoje.** Não há campo no protocolo, coluna no banco nem lugar na tela: começa
  no SentinelWeb.

## Ping

Só prova que a máquina está viva. Qualquer mensagem faz o mesmo, mas a telemetria é de hora em hora, e é o
`Ping` que mantém a máquina ONLINE entre uma amostra e outra. A máquina vira OFFLINE na hora em que a conexão
fecha, ou depois de **2 minutos** sem mensagem nenhuma (`mark-stale-endpoints-offline.usecase.ts`); o job
confere a cada 60 s.

- **Do lado do agente**
  ([A12](../decisoes/protocolo.md#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s)): o `Ping`
  vai quando a conexão fica calada por um tempo sorteado entre 30 e 60 s (`Connection/ConnectionTimings.cs`).

---

## Command (do servidor para o agente)

```json
{ "Type": "Command", "Payload": { "Action": "SHUTDOWN", "CommandId": "6f1c…" } }
```

- `Action` é `SHUTDOWN`, `REBOOT` ou `SUSPEND`, sempre em maiúsculas. O servidor só aceita esses três da tela
  (`server/modules/endpoint/schemas/endpoint.schema.ts`), e só de quem é **Supervisor** ou acima.
- `CommandId` é um UUID novo por comando, gravado na auditoria do servidor: é a chave para cruzar com o log
  do agente.
- **Não há fila.** Comando para máquina desconectada é recusado na hora (409), nunca executado depois.
- **Não há resposta.** O servidor diz "Comando enviado" quando a conexão aceitou os bytes; o agente não
  confirma, e mensagem de confirmação seria descartada como tipo desconhecido.

## Quando a conexão fecha

- **O servidor encerrando** fecha todas as conexões com o código 1001 ("Servidor encerrando"): o agente
  reconecta.
- **O agente encerrando** leva a máquina a OFFLINE na hora, a não ser que uma conexão mais nova da mesma
  máquina já a tenha substituído.
- **O HWID do handshake diferente do token** o servidor fecha com o código 1008 ("HWID diferente do token")
  ([CU](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/acesso.md#cu--a-conexão-com-o-hwid-de-outra-máquina-fecha-com-1008)):
  a placa ou o firmware mudou depois do cadastro, ou o token veio de outra máquina. O agente diz isso no log,
  espera o teto e não se recadastra sozinho; o recadastro é pôr o segredo de instalação de novo.
- **Do lado do agente** (`Connection/AgentConnection.cs`): a conexão que cai antes de 1 minuto conta como falha,
  e a espera seguinte dobra. O erro que não se conserta tentando de novo (a máquina sem HWID, o `CaFile`
  ilegível) para o serviço com código de saída 1, e o systemd vê a falha.
- **Com o segredo de instalação e o token no disco**, o cadastro que não dá certo (409, rede) não impede a
  conexão: o agente conecta com o token gravado, e o servidor diz se ele ainda vale.
