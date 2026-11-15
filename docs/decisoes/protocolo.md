# Decisões do protocolo

> O que o agente diz ao SentinelWeb. O contrato em si é do servidor e está em
> [`../referencia/protocolo.md`](../referencia/protocolo.md); aqui fica o que o agente escolheu dentro dele.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve**. Se a realidade mudou, a decisão é
> **superada** por outra, com link entre as duas. As supersessões estão em [`README.md`](./README.md).
>
> Decisões neste arquivo: A4, A9, A10, A11, A12, A13 e A14.

---

## A4 — O HWID continua o do agente antigo

> **Superada em parte por [A13](#a13--o-hwid-é-uma-fila-de-três-dados-do-firmware):** a conta continua, mas a
> lista de seriais genéricos passa a ser a do SentinelWeb, e o UUID do firmware entra quando os dois seriais
> são genéricos.

**Decidido** (2026-11-13): o HWID é a mesma conta do agente antigo (`HwidGenerator` e
`ProcHardwareProvider`):

1. lê `/sys/class/dmi/id/product_serial`; se vier genérico, `/sys/class/dmi/id/board_serial`;
2. genérico é: vazio, com 3 caracteres ou menos, ou um destes, sem diferença de maiúscula: `None`,
   `Default string`, `Not Specified`, `To be filled by O.E.M.`, `System Serial Number`, `00000000`, `Unknown`;
3. tira `-`, `:` e espaço e passa para minúsculas;
4. o HWID é o SHA-256 disso, em hexadecimal minúsculo.

**Descartado:**
- **O `/etc/machine-id`**, proposto no primeiro desenho desta estrutura: ele nasce na instalação do sistema, e
  reinstalar a máquina daria outro HWID — o contrário do que o SentinelWeb assume
  ([H](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/reconciliacao.md#h--a-fusão-de-máquinas-sai):
  "o hwid é derivado do hardware e estável entre reinstalações").
- **Trocar a conta por uma melhor** (o UUID primeiro, ou a lista de genéricos do SentinelWeb): qualquer
  mudança muda o HWID das máquinas já cadastradas, e cada uma vira uma linha nova em `endpoints`, sem o
  vínculo com o computador.

**Por quê:** o HWID é a chave da máquina no SentinelWeb (`endpoints.hwid`, único), e o artigo do projeto o
descreve assim: "o SHA-256 do número de série do firmware". Mantê-lo deixa o agente novo entrar no lugar do
antigo sem o servidor perceber a troca.

**O que fica de fora, declarado:**
- **A máquina sem série válida não sobe**, como no antigo: sem o que derivar, o agente para com erro em vez de
  inventar uma identidade. Boa parte das máquinas virtuais cai aqui. Um segundo recurso (o UUID, só quando
  não há série) não muda o HWID de quem já sobe, e é decisão à parte.
- **A lista de genéricos é menor que a do
  [D46](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/reconciliacao.md#d46--evidência-ambígua-é-evidência-zero)
  do SentinelWeb:** falta `0123456789`, e as máquinas com essa série ganham todas o mesmo HWID. Corrigir muda
  o HWID delas, então também é decisão à parte.

---

## A9 — O buffer offline volta, com a hora da coleta

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
(decisão G) e não volta por esta decisão.

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
- **Um token por máquina, pelo cadastro** (a decisão
  [CR](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/acesso.md#cr--cada-máquina-tem-o-seu-token-e-o-segredo-único-só-cadastra)
  do SentinelWeb). A instalação põe o segredo de instalação (o `AGENT_ENROLL_SECRET` do servidor) em
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
- **Um token só para a frota**, o `AGENT_TOKEN` que o SentinelWeb tinha
  ([D149](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/escopo.md#d149--o-apitoken-sai-e-o-agente-volta-ao-agent_token)).
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
- **O limite do OFFLINE é do servidor**, a decisão
  [CS](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/reconciliacao.md#cs--a-máquina-calada-vira-offline-em-2-minutos)
  do SentinelWeb, que diz também por que não há estado de "sumido".
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

**Decidido** (2026-11-15): o HWID é o SHA-256 do primeiro dado do firmware que não for falso, nesta ordem:

1. `/sys/class/dmi/id/product_serial`, o serial do produto;
2. `/sys/class/dmi/id/board_serial`, o serial da placa-mãe;
3. `/sys/class/dmi/id/product_uuid`, o UUID do firmware.

- **Serial falso** é o da lista do SentinelWeb (`server/modules/reconciliation/helpers/normalize-identity.helper.ts`,
  a [D46](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/reconciliacao.md#d46--evidência-ambígua-é-evidência-zero)
  em código): 18 textos de fábrica, menos de 4 caracteres ou um caractere repetido.
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
  pontos, e não 100, por causa da dock (D46). O MAC continua no handshake como dado.
- **Só o UUID**, como o osquery e o Fleet: uma regra a menos, mas o HWID de toda máquina com serial mudaria, e o
  artigo descreve o HWID como "o SHA-256 do número de série do firmware".
- **Número sorteado na instalação ou `/etc/machine-id`**, como Tactical RMM, Netdata e Elastic: mudam quando o
  sistema é reinstalado.

**Por quê:** só o que está gravado no firmware sobrevive à reinstalação, e é o que os projetos que usam hardware
leem:

| Peça | De onde vem |
|---|---|
| serial do produto, depois o da placa | systemd (`src/hostname/hostnamed.c`: "the product serial number (DMI type 1) is preferred, unless hwdb flags it as rubbish, in which case the baseboard serial number (DMI type 2) is used instead") |
| o UUID do firmware | osquery (`osquery/core/system.cpp`, `getHardwareUUID`) e Fleet (`host-identifier` `uuid`, no orbit) |
| a lista de UUIDs falsos | osquery (`kPlaceholderHardwareUUIDList`) e SentinelWeb (`normalize-identity.helper.ts`) |
| a lista de seriais falsos e o caractere repetido | SentinelWeb (D46) e Fleet (`server/fleet/hosts.go`, `IsPlaceholderHardwareSerial`) |

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
  `removable=0`, multipath), nenhuma tela do SentinelWeb o mostra (decisão
  [BY](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/reconciliacao.md#by--a-aba-máquina-mostra-o-que-o-agente-manda)),
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
