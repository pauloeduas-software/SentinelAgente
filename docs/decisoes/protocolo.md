# Decisões do protocolo

> O que o agente diz ao SentinelWeb. O contrato em si é do servidor e está em
> [`../referencia/protocolo.md`](../referencia/protocolo.md); aqui fica o que o agente escolheu dentro dele.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve**. Se a realidade mudou, a decisão é
> **superada** por outra, com link entre as duas. As supersessões estão em [`README.md`](./README.md).
>
> Decisões neste arquivo: A4, A9, A10, A11, A12, A13, A14, A15, A16, A20, A22, A24, A25, A26 e A28 (a A4
> superada pela A20 e em parte pela A13; a A9 pela A17; a A10 pela A24 e em parte pela A22; a A11 em parte pela
> A12; a A13 pela A20 e em parte pela A15; a A14 em parte pela A15, pela A20, pela A24 e pela A25; a A15 em
> parte pela A20; a A16 em parte pela A22 e pela A24; a A22 em parte pela A24 e pela A28; e a A25 em parte pela
> A26).

---

## A4 — O HWID continua o do agente antigo

> **Superada em parte por [A13](#a13--o-hwid-é-uma-fila-de-três-dados-do-firmware):** a conta continua, mas a
> lista de seriais genéricos passa a ser a do SentinelWeb, e o UUID do firmware entra quando os dois seriais
> são genéricos.
>
> **Superada por [A20](#a20--o-hwid-é-o-id-da-instalação-e-quem-reconhece-a-máquina-é-o-sentinelweb-como-no-glpi):** o HWID deixa de vir do firmware; é um UUID sorteado na instalação.

**Decidido** (2026-11-13): o HWID é a mesma conta do agente antigo (`HwidGenerator` e
`ProcHardwareProvider`):

1. lê `/sys/class/dmi/id/product_serial`; se vier genérico, `/sys/class/dmi/id/board_serial`;
2. genérico é: vazio, com 3 caracteres ou menos, ou um destes, sem diferença de maiúscula: `None`,
   `Default string`, `Not Specified`, `To be filled by O.E.M.`, `System Serial Number`, `00000000`, `Unknown`;
3. tira `-`, `:` e espaço e passa para minúsculas;
4. o HWID é o SHA-256 disso, em hexadecimal minúsculo.

**Descartado:**
- **O `/etc/machine-id`**, proposto no primeiro desenho desta estrutura: ele nasce na instalação do sistema, e
  reinstalar a máquina daria outro HWID — o contrário do que o SentinelWeb assumia: o HWID
  derivado do hardware e estável entre reinstalações.
- **Trocar a conta por uma melhor** (o UUID primeiro, ou a lista de genéricos do SentinelWeb): qualquer
  mudança muda o HWID das máquinas já cadastradas, e cada uma vira uma linha nova em `endpoints`, sem o
  vínculo com o computador.

**Por quê:** o HWID é a chave da máquina no SentinelWeb (`endpoints.hwid`, único). Mantê-lo deixa o agente
novo entrar no lugar do antigo sem o servidor perceber a troca.

**O que fica de fora, declarado:**
- **A máquina sem série válida não sobe**, como no antigo: sem o que derivar, o agente para com erro em vez de
  inventar uma identidade. Boa parte das máquinas virtuais cai aqui. Um segundo recurso (o UUID, só quando
  não há série) não muda o HWID de quem já sobe, e é decisão à parte.
- **A lista de genéricos é menor que a do SentinelWeb:** falta `0123456789`, e as máquinas com essa série
  ganham todas o mesmo HWID. Corrigir muda o HWID delas, então também é decisão à parte.

---

## A9 — O buffer offline volta, com a hora da coleta

> **Superada por [A17](./escopo.md#a17--sem-buffer-offline-a-amostra-sem-conexão-é-descartada):** o buffer não vale o custo com uma amostra por hora; a amostra sem conexão é descartada.

**Decidido** (2026-11-13):
- Sem conexão, o agente guarda as amostras de telemetria numa fila (`Buffer/`) e manda na reconexão, depois do
  handshake, da mais velha para a mais nova.
- **Toda amostra leva a hora em que foi coletada**, e não só a guardada: um campo novo no `Telemetry`.
- A fila tem limite; cheia, a amostra mais velha sai.

**Supera:** [A7](./escopo.md#a7--a-telemetria-não-tem-buffer-offline).

**Por quê:** a queda de rede não deve virar buraco no histórico da máquina. O que a A7 apontou continua
verdade: com a hora da chegada, as amostras guardadas seriam gravadas todas no instante da reconexão. Por isso
o buffer volta junto com a hora da coleta.

**Depende do SentinelWeb:** hoje o servidor grava `Telemetry.timestamp` com a hora da chegada
(`@default(now())`) e não tem o campo no protocolo. **Antes de o agente ligar o buffer, o servidor passa a
ler a hora da coleta** (e a recusar a que vier no futuro). Até lá, a amostra sem conexão é descartada, como na
A7.

**O que fica de fora, declarado:** o tamanho da fila e se ela sobrevive a um reinício do agente (memória ou
disco). O antigo guardava 10 amostras em memória, a cada 30 s: cinco minutos.

---

## A10 — A lista de programas instalados volta

> **Superada em parte por [A22](#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar):**
> o agente manda a lista no handshake sem esperar o servidor guardar.
>
> **Superada por [A24](#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem):** a lista de programas sai de novo, até o SentinelWeb guardar e mostrar.

**Decidido** (2026-11-13): o agente volta a mandar os programas instalados (`Software/`), lidos do gerenciador
de pacotes — `dpkg-query` no Debian e Ubuntu, `rpm` no Fedora e Red Hat —, com nome e versão. O usuário logado
continua fora.

**Supera em parte:** [A8](./escopo.md#a8--o-handshake-não-manda-programas-instalados-nem-usuário-logado) (os
programas instalados).

**Por quê:** é o inventário de software, que o GLPI e o OCS também coletam. No antigo a lista nunca funcionou:
`GetInstalledSoftware` devolvia o texto fixo "Pending dpkg/rpm parser".

**Depende do SentinelWeb:** hoje o servidor aceita `InstalledSoftware` e descarta (`normalize-payload.helper.ts`),
e não tem tabela nem tela para isso. Guardar e mostrar começa lá. Se a lista vai no handshake, como no
antigo, ou numa mensagem própria (ela é a parte mais pesada, e muda pouco), decide-se junto com o servidor.

**O que fica de fora, declarado:** conformidade de licença (instalado × comprado), que o SentinelWeb tirou
e não volta por esta decisão.

---

## A11 — A conexão: endereço na configuração, `wss://` e um token por máquina

> **Superada em parte por [A12](#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s):** o `Ping` vai
> a cada 30 a 60 s, e o servidor põe a máquina OFFLINE após 2 min sem mensagem.

**Decidido** (2026-11-13):
- **O endereço vem da configuração**, nunca do código: `Server:Url` em `/etc/sentinel-agent/agent.json`,
  que a instalação escreve. Endereço ausente ou inválido para o serviço na subida, com o motivo no journal.
- **`wss://` é obrigatório.** `ws://` só vale para o próprio computador (`localhost`), em desenvolvimento;
  para outro endereço o agente não sobe. Não há opção que desligue a verificação do certificado.
- **CA própria, opcional:** `Server:CaFile` aponta um PEM, e aí só essa CA vale, sem as do sistema. É o
  caminho para servidor com certificado interno ou autoassinado. O nome do certificado continua conferido.
- **Um token por máquina, pelo cadastro.** A instalação põe o segredo de instalação (o `AGENT_ENROLL_SECRET`
  do servidor) em
  `Server:EnrollSecretFile` (`/etc/sentinel-agent/enroll-secret`). Achando esse arquivo, o agente chama
  `POST /agent-hub/enroll` com o HWID, grava o token que volta em `Server:TokenFile`
  (`/var/lib/sentinel-agent/token`) e **apaga o segredo**. O WebSocket abre com `Authorization: Bearer <token>`.
- **Os dois arquivos são `chmod 600`.** O agente recusa o que outro usuário lê, como o ssh faz com a chave
  privada, e grava o token num temporário que depois toma o lugar do definitivo: o agente que cai no meio não
  deixa token pela metade.
- **Token recusado (401) não se recadastra sozinho:** o log diz onde pôr o segredo de novo. Sem o segredo na
  máquina, revogar o token no servidor vale de verdade.
- **Reconexão sem desistir**, com espera que dobra de 5 s até 5 min, sorteada entre a metade e o valor cheio.
  Segredo ou token recusados esperam direto os 5 min.
- **A conexão se testa sozinha:** ping do WebSocket a cada 30 s, com 15 s para a resposta. E um `Ping` do
  protocolo depois de 1 min sem mandar nada, porque o SentinelWeb põe a máquina OFFLINE após 4 min sem mensagem.
- **Ao parar**, o agente fecha a conexão com 1000, e o servidor põe a máquina OFFLINE na hora.

**Descartado:**
- **O endereço fixo do antigo** (`ws://localhost:5000/agent-hub`, no `Program.cs`), sem token e sem TLS.
- **Um token só para a frota**, o `AGENT_TOKEN` que o SentinelWeb tinha.
  Com ele, quem tinha o token se apresentava com o HWID de outra máquina, tomava o lugar dela no servidor (a
  conexão nova substitui a velha) e passava a receber os comandos dela.
- **Guardar o segredo de instalação depois do cadastro**, como o Fleet faz para se recadastrar sozinho. Ele
  vale para a frota inteira: quem tomasse uma máquina cadastraria outras, e a revogação se desfaria no
  recadastro automático. É o caminho do Elastic Agent, que também não se recadastra sozinho.
- **Um `--insecure`**, como o do Fleet, do Tactical RMM e do Elastic Agent. Os três o descrevem como "só
  para teste", e nenhum impede que ele chegue à produção. Aqui o certificado próprio entra pelo `CaFile`.
- **Segredo no `agent.json` ou numa variável de ambiente.** O `agent.json` pode ser lido por todos, e a
  variável aparece no `systemctl show` e passa aos processos filhos.

**Por quê:** é o que os agentes olhados fazem, lido no código de cada um:

| Peça | De onde vem |
|---|---|
| endereço e segredo gravados pela instalação, num arquivo `0600` | Fleet (`orbit/pkg/packaging/linux_shared.go`: `/etc/default/orbit`), Elastic Agent (`fleet.enc`) |
| segredo de instalação trocado por um token da máquina | Fleet (`client/orbit_client.go`: enroll secret → node key), osquery (`enroll.cpp`: node_key), Elastic Agent (`enroll_cmd.go`: `access_api_key`), Tactical RMM (`install.go`: `/api/v3/newagent/`), Wazuh (`enrollment_op.c`: `client.keys`) |
| o token gravado num temporário e trocado de lugar | Fleet (`client/orbit_client.go`) |
| sem recadastro automático depois do 401 | Elastic Agent (`fleet_gateway.go`: após 6 recusas, uma tentativa por hora) |
| só TLS | osquery (`tls.cpp`: "Cannot create TLS request for non-HTTPS protocol URI"), Tactical RMM (`agent.go`: `tls://` ou `wss://`) |
| CA própria por arquivo | Fleet (`--fleet-certificate`), Tactical RMM (`--cert`), Elastic Agent (`--certificate-authorities`) |
| token no header do upgrade do WebSocket | Fleet (`orbit/pkg/wstransport/manager.go`: `Authorization: Node key …`) |
| espera dobrando, com sorteio | Elastic Agent (`pkg/backoff/equal_jitter.go`), Fleet (`orbit/pkg/backoff`: 5 s a 5 min) |
| ping de 30 s e 15 s de espera pela resposta | Fleet (`manager.go`) |

Dos sete agentes olhados, seis dão a cada máquina a sua credencial (o MeshAgent gera um certificado próprio);
só o Remotely não tem nenhuma. O agente roda como root e executa o que chega pela conexão: sem TLS, quem está
na mesma rede lê o token e manda desligar as máquinas; sem token por máquina, uma máquina comprometida fala por
todas.

**O que fica de fora, declarado:** a máquina **desligada** pode ser recadastrada por quem tem o segredo de
instalação (com ela conectada, o servidor responde 409, a regra do Wazuh). E a instalação que põe o segredo e
o `agent.json` é do pacote `.deb` ([A3](./plataforma.md#a3--o-agente-é-um-serviço-do-systemd-instalado-por-pacote)),
que ainda não existe.

---

## A12 — Os tempos: telemetria de hora em hora e `Ping` de 30 a 60 s

**Decidido** (2026-11-13), com o Tactical RMM e o Fleet como base:

| | Valor | De onde |
|---|---|---|
| Telemetria | **de hora em hora** | Fleet: `detail_update_interval` de 1 h (`server/config/config.go`) |
| `Ping` do protocolo | **sorteado entre 30 e 60 s** sem mandar nada | Tactical RMM: o `checkin_hello` de 30 a 60 s (`api/tacticalrmm/apiv3/utils.py`) |
| OFFLINE sem mensagem (servidor) | **2 min**: o máximo do `Ping` mais 60 s | Fleet: o intervalo mais `OnlineIntervalBuffer = 60` (`server/fleet/hosts.go`) |
| Teto da reconexão | 5 min (continua) | Fleet (`orbit/pkg/wstransport/manager.go`) |

- **O sorteio é um por ping:** a espera não passa do máximo, e a frota que reconectou junta não pinga junta.
- **O limite do OFFLINE é do servidor**, e é ele quem o decide.
- **Os tempos ficam no `ConnectionTimings`**, fora do `agent.json`; a telemetria entra quando o `Telemetry/`
  for escrito.

**Supera em parte:** [A11](#a11--a-conexão-endereço-na-configuração-wss-e-um-token-por-máquina), que mandava o
`Ping` depois de 1 min parado, com o OFFLINE em 4 min.

**Por quê:** os dois separam o sinal de vida, leve e frequente, da coleta pesada, que vai de vez em quando. Com
a telemetria de 30 s eram 2.880 linhas por máquina por dia; de hora em hora, 24. É o `Ping` que mantém a
máquina ONLINE entre uma amostra e outra.

**O que fica de fora, declarado:** o gráfico de CPU e memória do painel ganha um ponto por hora. E as
checagens do Tactical RMM (CPU, disco e serviço a cada 120 s, com alerta), que o agente não terá.

---

## A13 — O HWID é uma fila de três dados do firmware

> **Superada em parte por [A15](#a15--a-revisão-da-identidade-e-do-inventário-o-lixo-sem-separadores-a-lenovo-sempre-o-túnel-pelo-tipo):** o serial de fábrica se reconhece sem `-`, `:` e
> espaço, e as listas de seriais e de UUIDs de fábrica crescem.
>
> **Superada por [A20](#a20--o-hwid-é-o-id-da-instalação-e-quem-reconhece-a-máquina-é-o-sentinelweb-como-no-glpi):** o HWID é um UUID sorteado na instalação, e quem reconhece a máquina
> reinstalada é o SentinelWeb, pelo serial e pelo UUID. A fila sobrevive no `BiosSerial`: o do produto, senão o da placa.

**Decidido** (2026-11-15): o HWID é o SHA-256 do primeiro dado do firmware que não for falso, nesta ordem:

1. `/sys/class/dmi/id/product_serial`, o serial do produto;
2. `/sys/class/dmi/id/board_serial`, o serial da placa-mãe;
3. `/sys/class/dmi/id/product_uuid`, o UUID do firmware.

- **Serial falso** é o da lista do SentinelWeb (`server/modules/reconciliation/helpers/normalize-identity.helper.ts`):
  18 textos de fábrica, menos de 4 caracteres ou um caractere repetido.
- **UUID falso** é o que não tem formato de UUID ou é um dos cinco: os três do SentinelWeb (zeros, só `f` e
  `03000200-0400-0500-0006-000700080009`) e os dois que o osquery acrescenta (`kPlaceholderHardwareUUIDList`).
- **A conta continua a da A4:** tira `-`, `:` e espaço, passa para minúsculas e faz o SHA-256 em hexadecimal
  minúsculo.
- **Nada serve, o agente não sobe**, com os três valores lidos no journal. Não há número sorteado, machine-id
  nem MAC.
- **A fila só cresce no fim.** Degrau novo no fim só atende máquina que hoje não sobe; mexer no começo dá outro
  HWID a máquina cadastrada, e o servidor descarta mensagem com HWID diferente do token.

**Supera em parte:** [A4](#a4--o-hwid-continua-o-do-agente-antigo) (a lista de genéricos e a máquina sem
serial).

**Descartado:**
- **O MAC da placa de rede.** Ele é fixo na placa, mas a placa sai da máquina: Wi-Fi M.2, adaptador USB e dock.
  Nenhum dos projetos olhados o põe na identidade; o MeshAgent faz o contrário e troca a identidade quando o MAC
  some (`meshcore/agentcore.c`: "NodeID will reset, MAC Address Mismatch"). O próprio SentinelWeb dá ao MAC 85
  pontos, e não 100, por causa da dock. O MAC continua no handshake como dado.
- **Só o UUID**, como o osquery e o Fleet: uma regra a menos, mas o HWID de toda máquina com serial mudaria.
- **Número sorteado na instalação ou `/etc/machine-id`**, como Tactical RMM, Netdata e Elastic: mudam quando o
  sistema é reinstalado.

**Por quê:** só o que está gravado no firmware sobrevive à reinstalação, e é o que os projetos que usam hardware
leem:

| Peça | De onde vem |
|---|---|
| serial do produto, depois o da placa | systemd (`src/hostname/hostnamed.c`: "the product serial number (DMI type 1) is preferred, unless hwdb flags it as rubbish, in which case the baseboard serial number (DMI type 2) is used instead") |
| o UUID do firmware | osquery (`osquery/core/system.cpp`, `getHardwareUUID`) e Fleet (`host-identifier` `uuid`, no orbit) |
| a lista de UUIDs falsos | osquery (`kPlaceholderHardwareUUIDList`) e SentinelWeb (`normalize-identity.helper.ts`) |
| a lista de seriais falsos e o caractere repetido | SentinelWeb e Fleet (`server/fleet/hosts.go`, `IsPlaceholderHardwareSerial`) |

A lista maior só muda o HWID da máquina com serial que só ela reconhece, como `123456789`: com a lista da A4,
todas essas ganhavam o mesmo HWID e viravam uma linha só no painel. Nenhuma máquina tem ainda token deste agente,
então esta é a hora de fechar a conta.

**O que fica de fora, declarado:**
- **A máquina com os três dados de fábrica não sobe**, como o mini PC com a BIOS AMI padrão. Se for comum, o MAC
  de fábrica da placa de rede embutida pode virar um quarto degrau, sem mudar o HWID de ninguém.
- **VM clonada que copia o UUID** ganha o HWID da original, como já acontece com o serial copiado.
- **Ler os três pede root** (`0400` no kernel, `drivers/firmware/dmi-id.c`): rodado sem sudo, o agente para e
  diz isso.

---

## A14 — O inventário: o que fica estável vai, o que oscila ou não tem formato espera

> **Superada em parte por [A15](#a15--a-revisão-da-identidade-e-do-inventário-o-lixo-sem-separadores-a-lenovo-sempre-o-túnel-pelo-tipo):** na Lenovo, o modelo é sempre o `product_version`;
> o sistema sem `NAME` vai como "Linux" mais o `VERSION_ID`; e o túnel da VPN se reconhece pelo tipo da placa, e
> não pela falta de `device/`.
>
> **Superada em parte por [A20](#a20--o-hwid-é-o-id-da-instalação-e-quem-reconhece-a-máquina-é-o-sentinelweb-como-no-glpi):** o `BiosSerial` é o
> serial do produto, senão o da placa-mãe, e a máquina sem dado do firmware sobe.
>
> **Superada em parte por [A24](#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem):** o `RamTotalBytes` sai do handshake; a RAM vai só na telemetria.
>
> **Superada em parte por [A25](#a25--o-handshake-leva-a-versão-do-agente-e-vai-de-novo-uma-vez-por-dia):** o handshake vai de novo uma vez por dia, com a ficha relida, e leva a versão do agente.

**Decidido** (2026-11-15): o handshake leva a ficha da máquina, relida a cada conexão
(`Inventory/MachineInventory.cs`), com estas regras:

| Campo | De onde | A regra |
|---|---|---|
| `Hostname` | `/proc/sys/kernel/hostname` (o `gethostname`) | o nome curto, sem o domínio |
| `OsVersion` | `/etc/os-release` | **sempre vai**, como `NAME VERSION_ID` ("Ubuntu 24.04"); sem `VERSION_ID`, só o `NAME`; sem os-release, "Linux" |
| `CpuModel` | `/proc/cpuinfo` | o `model name`, com os espaços repetidos virando um; sem ele (ARM), nulo |
| `MacAddress`, `LocalIp` | `/proc/net/route` e `/sys/class/net` | a placa da rota padrão, física antes de virtual, e de menor métrica; o IPv4 dela na rede do gateway |
| `BiosSerial`, `SystemUuid` | `/sys/class/dmi/id/product_serial` e `product_uuid` | valor de fábrica (as listas da A13) vai nulo |
| `Manufacturer`, `HardwareModel` | `sys_vendor` e `product_name` | valor de fábrica (a lista do GLPI Agent) vai nulo; na Lenovo, o modelo é o `product_version` |
| `RamTotalBytes` | `/proc/meminfo` | o `MemTotal` arredondado para cima em GiB, em bytes |

E dois campos do protocolo o agente **não manda**: `ChassisType` e `DiskTotalBytes`.

**Por quê:** o SentinelWeb compara de `BiosSerial` para baixo, mais `CpuModel` e `OsVersion`, a cada handshake,
em texto exato, e a diferença vira uma mudança de hardware (`hardware-diff.helper.ts`). Valor que oscila sem a
máquina mudar é mudança falsa, então cada campo vai na forma que só muda quando a máquina muda:

- **O `PRETTY_NAME` leva a versão pontual** ("Ubuntu 24.04.1 LTS"), e cada atualização viraria uma mudança. O
  OCS e o Fleet também montam nome e versão (`NAME` + `VERSION_ID`); o Tactical RMM põe o kernel na string, e ela
  muda a cada atualização. E `OsVersion` ausente o servidor grava como "desconhecido" e conta como mudança
  (`normalize-payload.helper.ts`): por isso nunca vai nulo.
- **O `MemTotal` cru varia alguns kB** com o kernel. Os quatro projetos olhados (GLPI Agent, OCS, osquery e
  Tactical RMM) usam o `MemTotal`, e não a soma dos pentes do DMI; o Tactical arredonda para cima em GiB
  (`agent/utils.go`, `TotalRAM`).
- **A placa da rota padrão** é a escolha do Fleet (`networkInterfaceQuery`: rota padrão, menor métrica), e
  "física" é a do GLPI Agent (`Linux/Networks.pm`: tem `device/`). A VPN de túnel completo vira a rota de menor
  métrica e ficaria com o IP; com a física na frente, não. A bridge do servidor de VMs (`br0`) serve quando é a
  única rota. OCS e Tactical mandam todos os IPs, com o `docker0` no meio.
- **Valor de fábrica nulo** porque o servidor mantém o que já tinha quando o campo de identidade não vem
  (`register-handshake.usecase.ts`), e não tem lista de genéricos para fabricante e modelo. A troca da Lenovo é a
  do GLPI Agent, do OCS e do osquery.
- **`DiskTotalBytes` fica de fora** porque é o mais difícil de deixar estável (pendrive, HD USB com
  `removable=0`, multipath), nenhuma tela do SentinelWeb o mostra,
  e a telemetria já manda os discos um a um. Nenhum dos quatro projetos soma discos no agente.
- **`ChassisType` fica de fora** porque o formato não está definido no SentinelWeb: o teste de lá manda `"10"`,
  o código SMBIOS, e o agente falso manda `"Desktop"`.

**Descartado:**
- **O dmidecode:** o sysfs tem tudo o que o handshake leva, e uma dependência a menos no pacote.
- **Inventário periódico ou só do que mudou**, como o GLPI Agent e o OCS fazem com hash por seção: aqui o
  inventário vai no handshake, uma vez por conexão, e é pequeno.
- **A lista de todas as placas e discos:** o protocolo tem um MAC e um IP.
- **Tabela de peças para o processador ARM**, como a do gopsutil: o ARM sem DMI nem sobe (A13), e o com DMI é
  raro.

**O que fica de fora, declarado:**
- **A RAM é a que o sistema vê**, arredondada: 16 GiB com 1,5 GiB reservados para o vídeo integrado vão como
  15 GiB.
- **Sem rota padrão, o MAC e o IP não vão**, e o servidor apaga os que tinha.
- **`DiskTotalBytes` volta** quando o SentinelWeb mostrar o campo, e **`ChassisType`** quando o servidor fixar o
  formato.

---

## A15 — A revisão da identidade e do inventário: o lixo sem separadores, a Lenovo sempre, o túnel pelo tipo

> **Superada em parte por [A20](#a20--o-hwid-é-o-id-da-instalação-e-quem-reconhece-a-máquina-é-o-sentinelweb-como-no-glpi):** o HWID não tem mais hash; a lista de fábrica continua, e vale para o
> `BiosSerial` e o `SystemUuid` do handshake.

**Decidido** (2026-11-15, numa revisão do `Identity/` e do `Inventory/` contra o GLPI Agent, o osquery, o Fleet, o
systemd e o kernel):

- **O serial de fábrica se reconhece sem `-`, `:` e espaço**, a forma que vai para o hash. A lista, o corte de 4
  caracteres e o caractere repetido olham o valor compactado, e a lista ganha os sete do Fleet e do GLPI Agent, como o
  SentinelWeb já fazia.
- **A lista de UUIDs ganha os dois de fábrica da AMI na outra ordem de bytes:** no SMBIOS anterior ao 2.6, o kernel
  imprime o `product_uuid` com os primeiros campos invertidos (`dmi_save_uuid`, em `drivers/firmware/dmi_scan.c`).
- **Na Lenovo, o modelo é sempre o `product_version`** quando ele não é de fábrica, como no osquery
  (`system_info.cpp`) e no systemd (`ID_SYSFS_ATTRIBUTE_MODEL=product_version`). O filtro do GLPI Agent por "Think",
  "Idea"… deixava o "Lenovo V14 G2 ITL" ir como "82KB".
- **"Super Server" e "System Name"** entram nos nomes de fábrica, como o systemd os marca
  (`ID_PRODUCT_NAME_IS_RUBBISH`).
- **O sistema sem `NAME` é "Linux"** mais o `VERSION_ID`, o padrão do os-release, e não o `PRETTY_NAME`. O
  `VERSION_ID` que é data de snapshot (8 dígitos, o openSUSE Tumbleweed) não vai.
- **O túnel da VPN se reconhece pelo `/sys/class/net/<placa>/type`**, e não pela falta de `device/`: o que não é
  Ethernet (`tun`, `wg` e `ppp`) fica para trás, e bond, bridge e VLAN contam pela métrica.

**Supera em parte:** [A13](#a13--o-hwid-é-uma-fila-de-três-dados-do-firmware) (as listas e a comparação dos seriais
de fábrica) e [A14](#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera) (a troca da
Lenovo, o sistema sem `NAME` e "física antes de virtual").

**Por quê:** com a lista olhando o valor com separadores, `0000-0000` passava, e toda máquina com ele ganhava o mesmo
HWID: a segunda a se cadastrar tomava o lugar da primeira. Nenhuma máquina tem ainda token deste agente, e a A13 já
dizia que é a hora de fechar a conta: depois, mudar a lista muda o HWID de máquina cadastrada. Já "física antes de
virtual" mandava o Wi-Fi de gerência no lugar do `bond0` do servidor: bond e bridge não têm `device/`, mas é por
eles que a máquina sai.

**O que fica de fora, declarado:**
- **A VM com balão de memória** (`virtio_balloon`, a memória dinâmica do Hyper-V) muda o `MemTotal` com o balão, e o
  arredondamento não segura: cada GiB a mais ou a menos vira uma mudança no SentinelWeb.
- **O Wi-Fi com MAC aleatório** manda um MAC por conexão. O MAC não entra na comparação nem no vínculo, só na tela.

---

## A16 — A telemetria: a média desde a amostra anterior, o disco como o `df`, a rede só pela placa física

> **Superada em parte por [A22](#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar):**
> a GPU vai no handshake, como inventário; o uso dela continua fora.
>
> **Superada em parte por [A24](#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem):** o `Network` e o `TopProcesses` saem da telemetria.

**Decidido** (2026-11-15), com o Telegraf, o gopsutil e o Tactical RMM como base. Cada número da amostra de hora em
hora ([A12](#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s)) quer dizer isto:

| Campo | A regra | De onde |
|---|---|---|
| `CpuUsagePercentage` | a média desde a amostra anterior, pela diferença da linha `cpu` do `/proc/stat`; contador que volta para trás dá 0 | Telegraf (`plugins/inputs/cpu/cpu.go`: `lastStats`, `totalDelta`) |
| a primeira amostra | a leitura de partida e a primeira ficam a 10 s uma da outra | Tactical RMM (`agent/agent.go`: `cpu.Percent(10*time.Second)`) |
| `RamTotalBytes`, `RamUsedBytes` | o `MemTotal` cru, e o usado é `MemTotal − MemAvailable` | gopsutil (`mem/mem_linux.go`), o `free` |
| `Disks` | o `/proc/mounts`, só dispositivo local (`/dev/…` ou ZFS), sem `tmpfs`, `devtmpfs`, `devfs`, `iso9660`, `overlay`, `aufs` e `squashfs`, um ponto por dispositivo; o nome é o ponto de montagem, o total são os blocos e o usado é `Blocks − Bfree` | Telegraf (`plugins/inputs/disk/sample.conf`), gopsutil (`disk/disk_unix.go`), o `df` |
| `Network` | só as placas com `/sys/class/net/<placa>/device`; os bytes desde o boot, somados, e a velocidade média desde a amostra anterior | Telegraf (`plugins/inputs/net/net.go`: pula loopback) |
| `TopProcesses` | os 10 de maior `VmRSS`, um por PID, com o nome do `/proc/<pid>/comm`; sem RSS (thread de kernel), não entra | Tactical RMM (`agent/process.go`: `Resident`) |

- **A primeira amostra vai quando a conexão sobe pela primeira vez**, e depois uma por hora, com ou sem conexão.
  Sem conexão, a amostra é descartada ([A9](#a9--o-buffer-offline-volta-com-a-hora-da-coleta)), e a leitura
  anterior avança do mesmo jeito: a próxima é a média da última hora.
- **Coletor que falha não derruba o serviço:** a parte dele vai zerada, que é o que o servidor grava para o
  campo ausente, e o motivo vai para o log.

**Por quê:**
- **A média, e não o instante:** com uma amostra por hora, o uso de um segundo cai num pico ou num vale e não
  diz nada da hora. A média é o ponto que o gráfico do painel ganha.
- **A rede só pela placa física:** o que passa pelo túnel da VPN, pela bridge, pelo docker e pelas veth também
  passa pela placa física, e somar os dois contaria o mesmo byte duas vezes.
- **Um ponto por dispositivo:** o bind mount e o subvolume do btrfs mostram o mesmo espaço em dois lugares.
- **O usado como o `df`:** quem confere na máquina vê o mesmo número. A porcentagem que a tela calcula
  (usado ÷ total) fica um pouco abaixo da do `df`, que divide pelo que o usuário comum pode usar.
- **Um processo por PID:** o protocolo leva um `Pid` por item, e o Tactical RMM também não junta.

**Descartado:**
- **Juntar processos pelo nome** (as abas do Chrome numa linha só): pede mudar o significado do `Pid`, e isso
  começa no SentinelWeb.
- **Todas as placas de rede, como o Telegraf faz por padrão:** o protocolo tem um `Network` só, e a soma de
  todas conta o tráfego virtual duas vezes.

**O que fica de fora, declarado:**
- **A GPU:** o servidor não tem campo, coluna nem tela para ela; começa no SentinelWeb.
- **Compartilhamento de rede (NFS, CIFS) não entra nos discos:** o espaço é de outra máquina, e o servidor que
  caiu travaria a leitura.
- **O pendrive entra nos discos enquanto estiver montado.** A regra contra a oscilação é do handshake
  ([A14](#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera)); aqui os discos vão
  um a um, e nada se compara.
- **Uma placa que saiu e voltou** (o adaptador USB) não soma na velocidade daquela amostra.

---

## A20 — O HWID é o id da instalação, e quem reconhece a máquina é o SentinelWeb, como no GLPI

**Decidido** (2026-11-15), depois de olhar como os outros fazem:

- **O HWID é um UUID sorteado no primeiro arranque**, gravado em `/var/lib/sentinel-agent/hwid` (`0644`, num
  temporário que depois toma o lugar do definitivo), e lido de lá daí em diante (`Identity/Hwid.cs`). Ele não vem do
  hardware, e some com o disco formatado e com o `apt purge`: a instalação nova sorteia outro.
- **Quem diz que a máquina reinstalada é a mesma é o SentinelWeb**, pelo `BiosSerial` e pelo `SystemUuid` do
  handshake, com as regras de importação do GLPI. A linha antiga, com o vínculo e a telemetria, passa a ter o HWID
  novo.
- **O `BiosSerial` é o serial do produto, senão o da placa-mãe**, a troca que o GLPI faz com o `mserial`
  (`Blacklist::processBlackList`). No PC montado, só a placa tem serial, e sem ele a máquina reinstalada não seria
  reconhecida. Valor de fábrica continua nulo, com a lista da A15 (`Inventory/FactoryValues.cs`).
- **A máquina sem nenhum dado do firmware sobe**, sem `BiosSerial` nem `SystemUuid`: ela só não é reconhecida depois
  de formatada.
- **O arquivo mexido à mão não vira outra identidade em silêncio:** o agente para, e o log diz para apagá-lo. Apagado,
  ele sorteia outro, e o token, preso ao HWID antigo, é recusado com 1008 até o recadastro.

**Supera:** [A4](#a4--o-hwid-continua-o-do-agente-antigo), [A13](#a13--o-hwid-é-uma-fila-de-três-dados-do-firmware) e,
em parte, a [A14](#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera) e a
[A15](#a15--a-revisão-da-identidade-e-do-inventário-o-lixo-sem-separadores-a-lenovo-sempre-o-túnel-pelo-tipo).

**Por quê:** o HWID tirado do firmware não era imutável. Ele mudava com a placa-mãe, com o serial que a BIOS passa a
preencher e com cada revisão da regra, que já aconteceu uma vez (a A15), e cada mudança deixava duas linhas da mesma
máquina no SentinelWeb, sem ferramenta para juntá-las. Os sistemas de inventário olhados separam as duas perguntas:

| | Quem é o agente | Qual é o computador |
|---|---|---|
| GLPI | `agentid` e `deviceid` sorteados e guardados em disco (`lib/GLPI/Agent.pm`) | o servidor, pelas regras de importação: serial e UUID, depois o serial (`RuleImportAsset.xml`) |
| OCS | `DEVICEID` do nome e da hora, em `ocsinv.conf` | o servidor, pelo nome, serial, MAC e modelo juntos (`Duplicate.pm`) |
| Tactical RMM, Elastic, Wazuh, MeshCentral | um id sorteado ou dado pelo servidor | ninguém: reinstalar duplica |
| Fleet, osquery | o UUID do firmware | o mesmo UUID, que a VM clonada repete |

O jeito do GLPI é o do tipo do SentinelWeb (inventário de computadores), e o servidor decide com o parque inteiro à
vista: a lista de fábrica e os valores repetidos em várias máquinas, que o agente sozinho não enxerga.

**Descartado:**
- **Continuar com o HWID do firmware**, como o Fleet. Ele se apoia num valor que muda, e o Fleet mesmo precisa de
  `--host-identifier=instance` para a VM clonada.
- **O `/etc/machine-id`.** Ele também muda na reinstalação, e não traria nada que o UUID sorteado não traga.

**O que fica de fora, declarado:**
- **A troca de placa-mãe** vira máquina nova no SentinelWeb, como no GLPI, no Jamf e no Intune.
- **O disco clonado com o agente instalado** leva o HWID e o token juntos: as duas máquinas disputam a mesma conexão.
  O GLPI Agent também não o detecta. Quem monta imagem apaga `/var/lib/sentinel-agent` antes de gerá-la.

---

## A22 — Os programas e as placas de vídeo vão no handshake, como no GLPI, antes de o SentinelWeb guardar

> **Superada em parte por [A24](#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem):** o `InstalledSoftware` sai, e o `Gpus` fica só com `Name` e `MemoryBytes`.
>
> **Superada em parte por [A28](#a28--a-memória-da-placa-de-vídeo-a-dedicada-vai-a-integrada-não):** a NVIDIA passa a ir com a memória, pelo `nvidia-smi`, e a AMD integrada vai sem.

**Decidido** (2026-11-15): o handshake leva duas listas do inventário do GLPI Agent, lidas a cada conexão:

- **`InstalledSoftware`**, o `SOFTWARES` do GLPI: o `dpkg-query` e o `rpm -qa` (`Software/`), os dois que estiverem
  na máquina, com os campos de `Generic/Softwares/Deb.pm` e `RPM.pm`.

  | Campo | dpkg | rpm |
  |---|---|---|
  | `Name`, `Version`, `Arch` | `Package`, `Version`, `Architecture` | `NAME`, `VERSION-RELEASE`, `ARCH` |
  | `From` | `deb` | `rpm` |
  | `Publisher` | não vai | `VENDOR` |
  | `Category` | `Section` | `GROUP`, sem o `Unspecified` do Fedora |
  | `SizeBytes` | `Installed-Size` × 1024 | `SIZE` |
  | `InstallDate` | não vai (o dpkg não guarda) | `INSTALLTIME`, o dia no fuso da máquina |

  Do dpkg só vai o que está instalado de fato (o status que termina em ` installed`); do rpm, o `gpg-pubkey` fica de
  fora. A lista vai em ordem de nome, e sem gerenciador nenhum, não vai.
- **`Gpus`**, o `VIDEOS` do GLPI (`Generic/PCI/Videos.pm`): os dispositivos PCI de classe `0x03` do
  `/sys/bus/pci/devices` (`Inventory/GpuInfo.cs`), na ordem do slot, com `Name`, `Manufacturer`, `Chipset`,
  `MemoryBytes`, `PciSlot` e `PciId`. O nome e o chip saem do `pci.ids`, divididos no colchete como o GLPI faz
  ("Navi 22 [Radeon RX 6700 XT]"); sem o arquivo, o nome é o `PciId`. O `.deb` recomenda o pacote `pci.ids`.

**Supera em parte:** [A10](#a10--a-lista-de-programas-instalados-volta) (esperar o servidor guardar para mandar), a
[A16](#a16--a-telemetria-a-média-desde-a-amostra-anterior-o-disco-como-o-df-a-rede-só-pela-placa-física) (a GPU
fora) e a [A2](./plataforma.md#a2--um-projeto-com-uma-pasta-por-função) (a pasta `Telemetry/Gpu/`).

**Por quê:** o agente fecha antes do servidor, e o servidor já ignora o que não conhece: o `InstalledSoftware` ele
lê e descarta (`normalize-payload.helper.ts`), e o `Gpus` nem lê. Nenhum dos dois entra na comparação de hardware
(`hardware-diff.helper.ts`), então mandar agora não gera mudança falsa. Quando o SentinelWeb guardar, o dado já está
chegando, no formato do GLPI, que é o modelo do inventário de lá. A GPU vai como inventário, e não como uso: o GLPI
não mede uso, e numa máquina de escritório ele fica perto de zero.

**Descartado:**
- **O uso da GPU na telemetria**, que era o plano da pasta `Telemetry/Gpu/`: cada fabricante pede um caminho (o
  `gpu_busy_percent` da AMD, o `nvidia-smi` do driver fechado, o `perf` da Intel), para um número que diz pouco de
  hora em hora.
- **A distribuição como editor de todo pacote deb**, que é o que o GLPI faz (`lsb_release -i`): o Chrome sairia como
  "Ubuntu".
- **A memória pela janela do PCI**, que é o que o GLPI manda: dá 256 MB numa placa de 8 GB. Vai a VRAM do `amdgpu`
  (`mem_info_vram_total`); a NVIDIA e a Intel vão sem memória.
- **Uma mensagem própria para os programas.** A A10 deixava em aberto; o servidor lê o campo no handshake, e o GLPI
  manda os programas no inventário, junto com o resto.

**O que fica de fora, declarado:**
- **Snap, Flatpak e AppImage**, que o GLPI lê em módulos à parte: só o que o dpkg e o rpm conhecem.
- **A resolução da tela**, que o GLPI tira do `xrandr`: o serviço não tem tela.
- **Os nomes ficam no fio até o SentinelWeb fixar os dele.** Se lá o campo nascer com outro nome, o
  `Protocol/HandshakePayload.cs` muda junto.

---

## A24 — O agente manda só o que o SentinelWeb lê: os programas, a rede e os processos saem

**Decidido** (2026-11-15): o que o servidor não guarda, ou guarda sem uso, sai do fio enquanto o sistema é pequeno.

| Sai | Onde estava | Por quê |
|---|---|---|
| `InstalledSoftware` | handshake (`Software/`) | o servidor lê e descarta; era a parte mais pesada do handshake, com milhares de pacotes |
| `RamTotalBytes` | handshake (`Inventory/MemInfo.cs`) | o servidor não lê; a telemetria já leva a RAM total |
| `Gpus`: `Manufacturer`, `Chipset`, `PciSlot`, `PciId` | handshake | a aba Máquina mostra só o nome e a memória |
| `Network` | telemetria (`Telemetry/Network/`) | com uma amostra por hora, a velocidade é a média de uma hora inteira e não diz nada; os contadores desde o boot, ninguém lê |
| `TopProcesses` | telemetria (`Telemetry/Processes/`) | uma foto por hora não pega o momento da lentidão, só a última fica no servidor, e a lista mostra o que o usuário roda |

Fica: a identidade e o inventário (`Hwid`, `Hostname`, `OsVersion`, `CpuModel`, `MacAddress`, `LocalIp`, `BiosSerial`,
`SystemUuid`, `Manufacturer`, `HardwareModel` e `Gpus` com `Name` e `MemoryBytes`), a CPU, a RAM e os discos da
telemetria, o `Ping` e os três comandos.

**Supera:** [A10](#a10--a-lista-de-programas-instalados-volta) e, em parte, a
[A2](./plataforma.md#a2--um-projeto-com-uma-pasta-por-função), a
[A8](./escopo.md#a8--o-handshake-não-manda-programas-instalados-nem-usuário-logado), a
[A14](#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera), a
[A16](#a16--a-telemetria-a-média-desde-a-amostra-anterior-o-disco-como-o-df-a-rede-só-pela-placa-física) e a
[A22](#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar).

**Por quê:** cada campo é código, testdata e uma regra para manter, e mandar "para o dia em que o servidor guardar"
(a A22) é pagar agora por um dia que não tem data. Programas, rede e processos têm uso na indústria (licença e
vulnerabilidade, tráfego, lentidão), mas o uso pede o que o sistema ainda não tem: uma tela que guarde os
programas, e uma amostra de minuto em minuto para a rede e os processos.

**O que fica de fora, declarado:**
- **A tela do SentinelWeb** mostra rede zerada e a lista de processos vazia, o que o servidor grava para o campo
  ausente, até sair de lá também.
- **A volta começa no SentinelWeb:** o servidor guarda e mostra, e o agente passa a mandar. O código que saiu está
  no `git log`, no formato do GLPI.

---

## A25 — O handshake leva a versão do agente, e vai de novo uma vez por dia

> **Superada em parte por [A26](#a26--o-refresh-manda-o-inventário-e-a-telemetria-na-hora-a-pedido-do-painel):** o "inventariar agora", descartado aqui, volta como o `REFRESH`, tratado pela conexão.

**Decidido** (2026-11-16, junto com o SentinelWeb):

- **O handshake leva `AgentVersion`:** a `<Version>` do csproj, a do pacote .deb, sem o `+commit` que o SDK põe na
  versão informativa (`Inventory/AgentVersion.cs`). Vai sempre. O SentinelWeb grava em `endpoints.agentVersion` e
  mostra na lista de Agentes e na aba Máquina.
- **Com a conexão de pé, o handshake vai de novo a cada 24 horas**, com o inventário relido
  (`ConnectionTimings.InventoryInterval`). Na mesma conexão: o servidor só atualiza a máquina. A leitura que falha
  vai para o log e tenta no dia seguinte, sem derrubar a conexão.
- **O 1008 leva o motivo do servidor para o log.** Ele fechava só pelo HWID diferente do token, e agora fecha
  também a máquina excluída em Agentes; o log dizia sempre "HWID".

**Supera em parte:** [A14](#a14--o-inventário-o-que-fica-estável-vai-o-que-oscila-ou-não-tem-formato-espera) (o
inventário só a cada conexão).

**Por quê:**
- **A versão serve para achar quem ficou com o agente velho** depois de uma atualização: o GLPI, o Fleet e o
  NinjaOne mostram a coluna. Sem ela, a única pista era o comportamento.
- **Numa máquina sempre ligada, a conexão dura semanas,** e a memória trocada, o IP novo ou o nome mudado só
  chegavam na próxima queda. O GLPI Agent manda o inventário uma vez por dia; o Intune tem o "Sync" para o mesmo
  fim. O reenvio diário resolve sem ninguém lembrar de clicar.

**Descartado:**
- **Um comando "inventariar agora"** do painel: a lista de comandos fica nas três ações de energia
  ([A23](./comandos.md#a23--a-lista-fica-nas-três-ações-e-o-terminal-não-entra)), e o
  reenvio diário cobre o caso.
- **O sorteio do horário do reenvio,** como o do `Ping`: a frota que reconecta junto reenvia junto 24 horas depois, e
  um handshake por máquina por dia não pesa no servidor.

---

## A26 — O `REFRESH` manda o inventário e a telemetria na hora, a pedido do painel

**Decidido** (2026-11-17, junto com o SentinelWeb):

- **O servidor manda o `Command` com a `Action` `REFRESH`,** e o agente responde com o handshake, o inventário relido
  como no reenvio diário, e com uma amostra de telemetria na hora. É a conexão que trata o pedido, e ele nunca chega
  ao `ICommandHandler`: não roda programa nenhum, só lê o que o handshake e a telemetria já leem.
- **A conexão avisa a telemetria pelo `IServerLink.RefreshRequested`,** uma tarefa que cada pedido completa e troca. A
  telemetria espera por ela junto com o relógio da hora, que não muda; o pedido na janela de partida encurta a janela,
  e a média da primeira amostra fica mais curta.
- **A versão sobe para 0.2.0.** O SentinelWeb compara a `AgentVersion` e não manda o `REFRESH` ao agente anterior, que o
  recusaria no log.

**Supera em parte:** [A5](./comandos.md#a5--comando-é-lista-fechada-sem-shell) (outra `Action` recusada: o
`REFRESH` não é), [A23](./comandos.md#a23--a-lista-fica-nas-três-ações-e-o-terminal-não-entra) (pedir o inventário
na hora, descartado ali) e [A25](#a25--o-handshake-leva-a-versão-do-agente-e-vai-de-novo-uma-vez-por-dia) (o
"inventariar agora" descartado).

**Por quê:** a A23 o descartou porque o painel não tinha pedido e não saberia se ele rodou. O painel pediu,
comparando Agentes com o GLPI ("Request inventory"), o Fleet ("Refetch") e o Intune ("Sync"), e a resposta é o próprio
handshake: o SentinelWeb espera por ele e pela telemetria antes de dizer "Máquina atualizada.". A preocupação da A23
com mais um jeito de mandar como root não se aplica: o pedido só faz o agente ler e mandar.

**O que não muda:** a lista fechada dos comandos de sistema continua com as três ações de energia (A23).

---

## A28 — A memória da placa de vídeo: a dedicada vai, a integrada não

**Decidido** (2026-11-18): o `MemoryBytes` de cada placa segue uma regra só, a placa dedicada vai com a memória dela
e a integrada vai sem, porque não tem memória própria.

| Placa | `MemoryBytes` | De onde |
|---|---|---|
| AMD dedicada | vai | `mem_info_vram_total` do `amdgpu`, como antes |
| NVIDIA com o driver fechado | vai | `/usr/bin/nvidia-smi --query-gpu=pci.bus_id,memory.total --format=csv,noheader,nounits`, casada pelo slot (`Inventory/NvidiaSmi.cs`) |
| AMD integrada | não vai | o `amdgpu` mostra nela a reserva da BIOS; ela é reconhecida pelo sensor `vddnb` (`hwmon/*/in1_label`), que o driver só cria na integrada |
| Intel integrada | não vai | não tem memória própria, e o `i915` não mostra a reserva |
| NVIDIA com o `nouveau` | não vai | o driver não mostra a memória |

O `nvidia-smi` só roda quando há uma placa NVIDIA, sem shell, com dez segundos para responder; sem ele, preso ou com
erro, a placa vai sem memória.

**Supera em parte:** [A22](#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar) (a NVIDIA sem memória).

**Por quê:** mandar a memória de uma marca e não de outra parecia regra, e era só o arquivo que existia. Nos agentes
de referência:
- **O OCS Inventory** lê a memória da NVIDIA pelo mesmo `nvidia-smi --query-gpu=memory.total`
  (`Backend/OS/Linux/Videos.pm`), e não lê a das outras. Aqui vai também o `pci.bus_id`: o OCS junta as linhas de
  todas as placas num valor só.
- **O GLPI Agent** manda a janela do PCI (`Generic/PCI/Videos.pm`), que a A22 já descartou, e procura a memória no log
  do Xorg (`Linux/Videos.pm`), que o serviço não tem.
- **No Windows** os dois têm um lugar só, o `HardwareInformation.qwMemorySize` do registro, que todo driver grava; no
  Linux cada driver mostra a memória do seu jeito, ou não mostra.

A integrada vai sem memória, e não com a reserva da BIOS que o Windows chama de dedicada: a reserva é RAM, que a
telemetria já conta, e o número muda com a configuração da BIOS, não com a placa.

**Descartado:**
- **A NVML** (`libnvidia-ml.so`), que o `nvidia-smi` usa por baixo: o mesmo dado, com uma biblioteca carregada no
  processo do agente em vez de um programa que termina.
- **Reconhecer a AMD integrada pela lista de ids do PCI** ou pelo tamanho da reserva: a lista envelhece a cada
  geração, e há placa dedicada pequena.

**O que fica de fora, declarado:**
- **A AMD integrada com o gerenciamento de energia desligado** (`amdgpu.dpm=0`) não tem o `hwmon`, e vai com a
  reserva da BIOS.
- **A Intel dedicada** (Arc) vai sem memória, como a integrada: o `i915` não mostra a dela.
- **A NVIDIA com o driver fechado e sem o `nvidia-smi`** vai sem memória.
