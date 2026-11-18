# Decisões da plataforma

> A linguagem, as pastas e como o agente chega à máquina.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve**. Se a realidade mudou, a decisão é
> **superada** por outra, com link entre as duas. As supersessões estão em [`README.md`](./README.md).
>
> Decisões neste arquivo: A1–A3, A19 e A21 (a A3 superada em parte pela A19, e a A2 em parte pela A21).

---

## A1 — O agente recomeça do zero, em C# com .NET 10

**Decidido** (2026-11-13): o agente é reescrito do zero neste repositório, em C# com .NET 10. O repositório
antigo (`pauloeduas-software/SentinelAgent`) fica só como consulta; o que não volta dele está em
[`escopo.md`](./escopo.md).

**Descartado:**
- **Consertar o agente antigo.** Além do que está no escopo, ele divergia do contrato do servidor: o endereço
  fixo em `ws://localhost:5000/agent-hub` (`Program.cs`), a conexão sem `Authorization: Bearer`, o total de
  rede em gigabits num campo `TotalRxGb` e o handshake sem série, UUID, fabricante, modelo, gabinete e
  totais — o que levou o SentinelWeb a tirar as especificações da tela.
  E só instalava pelo próprio binário (`--install` no `Program.cs`), sem jeito de desinstalar; `install.sh`,
  `install.ps1` e `SystemdInstaller.cs` estão vazios.
- **Go.** É a linguagem dos agentes de referência (Tactical RMM, Fleet, Telegraf): binário pequeno, sem
  runtime, com biblioteca pronta de métricas. Mas o artigo e a especificação de requisitos do projeto dizem
  C#, e para o tamanho do projeto a vantagem não paga reescrever os dois.
- **.NET 8**, que o artigo cita: o suporte terminou em 10/11/2026. O 10 é a versão LTS em vigor.

**Por quê:** o que prende a linguagem são os documentos do projeto, não o código antigo — e o C# cobre tudo
o que se pede do agente: serviço do systemd, `/proc` e `/sys`, WebSocket e os comandos de energia.

**O custo:** o artigo troca ".NET 8" por ".NET 10".

---

## A2 — Um projeto, com uma pasta por função

> **Superada em parte por [A21](#a21--a-pasta-service-sai-a-vida-do-serviço-é-do-addsystemd):**
> a pasta `Service` sai; a vida do serviço fica no `AddSystemd()` do `Program.cs`.
>
> **Superada em parte por [A22](./protocolo.md#a22--os-programas-e-as-placas-de-vídeo-vão-no-handshake-como-no-glpi-antes-de-o-sentinelweb-guardar):**
> a pasta `Telemetry/Gpu` sai; as placas de vídeo são lidas no `Inventory`.

**Decidido** (2026-11-13): um projeto (`SentinelAgente/`, na raiz do repositório, sem `src/`) com uma pasta por função direto na raiz dele —
`Config`, `Connection`, `Protocol`, `Identity`, `Inventory`, `Telemetry`, `Commands` e `Service` —, e dentro de
`Telemetry` uma pasta por métrica (`Cpu`, `Memory`, `Disk`, `Network`, `Processes`, `Gpu`). Um projeto de teste
com as mesmas pastas e `testdata/`. A regra está em
[`../referencia/arquitetura.md`](../referencia/arquitetura.md).

**Descartado:**
- **Os quatro projetos do antigo** (`Core`, `Linux`, `Worker` e `Shared`). A separação por sistema operacional
  só se paga com Windows, e a especificação diz Linux; o `Shared` dividia os pacotes com um servidor .NET, e o
  servidor é TypeScript.
- **Uma camada por cima das funções** (`Infra/` e `Modules/`, como o `server/` do SentinelWeb): esconde as
  funções um nível abaixo sem separar nada que as pastas já não separem.
- **Pastas por tipo de arquivo** (`Interfaces/`, `Models/`, `Services/`), como o agente do Remotely: quem
  procura como o comando funciona teria de abrir três pastas.

**Por quê:** é o que os projetos olhados fazem, cada um à sua maneira:

| Peça | De onde vem |
|---|---|
| uma pasta por função | Fleet (`orbit/pkg/osservice`, `…/scripts`, `…/backoff`, `…/packaging`) |
| um coletor por métrica | Telegraf (`plugins/inputs/cpu`, `mem`, `disk`, `net`) |
| a conexão separada das mensagens | Remotely (`AgentHubConnection.cs`) e Tactical RMM (`shared/types.go`) |
| os testes lendo cópias de `/proc` | Telegraf (`plugins/inputs/mem/testdata`, `…/disk/testdata`) |
| a instalação fora do código | Telegraf (`scripts/telegraf.service`, `scripts/deb`, `scripts/rpm`) e Fleet (`orbit/pkg/packaging`) |

**O que fica de fora, declarado:** Windows. Cada função ganharia a sua versão ao lado da do Linux, mas nada
foi pensado ainda contra ele.

---

## A3 — O agente é um serviço do systemd, instalado por pacote

> **Superada em parte por [A19](#a19--o-deb-runtime-do-ubuntu-endereço-e-segredo-nas-variáveis-da-instalação):**
> o pacote não traz a configuração; o `agent.json` e o segredo são escritos pela instalação, a partir de
> variáveis, e o serviço só liga quando o `agent.json` existe.

**Decidido** (2026-11-13):
- O agente roda como o serviço `sentinel-agent`, como root: ler a série e o UUID em `/sys/class/dmi/id` e
  desligar a máquina pedem root.
- **Sobe com a máquina:** a unidade fica habilitada no boot e o systemd o reinicia se cair.
- **Instala e desinstala com sudo:** o pacote `.deb` em `packaging/` traz o binário, a unidade e a
  configuração; `sudo apt install` instala, habilita e liga, e `sudo apt remove` para, desabilita e remove.
- O `Program.cs` usa a integração do .NET com o systemd (`Microsoft.Extensions.Hosting.Systemd`): avisa
  quando está pronto, para quando o systemd manda e escreve o log no journal.

**Descartado:**
- **O `--install` do agente antigo**, em que o binário grava `/etc/systemd/system/sentinel-agent.service`,
  habilita e liga o serviço. Instala, mas não desinstala, e a unidade é texto dentro do código. O nome do
  serviço (`sentinel-agent`) continua o mesmo.
- **Um `install.sh` solto.** Ele copia arquivos, mas não sabe remover o que pôs, e o gerenciador de pacotes já
  faz as duas coisas.

**Por quê:** é como os agentes olhados chegam à máquina — o Telegraf com `scripts/telegraf.service` e os
pacotes em `scripts/deb` e `scripts/rpm`, o Fleet com `orbit/pkg/packaging` (`deb.go`, `rpm.go`). Só root
instala ou remove pacote, e só root para um serviço do sistema: o requisito "só desinstala com sudo" é o
comportamento normal do Linux, sem nada escondido
([A6](./escopo.md#a6--nada-escondido-camuflagem-proteção-do-binário-e-driver-de-kernel-saem)).

**O que fica de fora, declarado:** `.rpm` (Fedora, Red Hat). A mesma configuração de pacote gera os dois
quando for preciso.

---

## A19 — O `.deb`: runtime do Ubuntu, endereço e segredo nas variáveis da instalação

**Decidido** (2026-11-15), completando a
[A3](#a3--o-agente-é-um-serviço-do-systemd-instalado-por-pacote):

**Como se instala:**

```bash
sudo SENTINEL_URL=wss://<servidor>/agent-hub SENTINEL_ENROLL_SECRET=<segredo> apt install ./sentinel-agent_<versão>_amd64.deb
```

- **O endereço e o segredo vêm de variáveis da instalação**, que o `postinst` lê, como no Wazuh
  (`WAZUH_MANAGER`, `WAZUH_ENROLLMENT_TOKEN`) e no Datadog (`DD_API_KEY`). O `postinst` escreve
  `/etc/sentinel-agent/agent.json` (`0644`) com o `Server:Url` e `/etc/sentinel-agent/enroll-secret` (`0600`).
  O agente troca o segredo pelo token e o apaga
  ([A11](./protocolo.md#a11--a-conexão-endereço-na-configuração-wss-e-um-token-por-máquina)). O pacote é o
  mesmo para todos os sites.
- **O `agent.json` é da máquina, não do pacote.** Ele não vem dentro do `.deb`: o `postinst` o escreve só
  quando ele não existe. A atualização não mexe nele, e o dpkg não pergunta nada. É o caminho do Telegraf e do
  Datadog, que geram a configuração na instalação em vez de entregá-la como conffile. Para trocar o endereço,
  edita-se o arquivo. Já o segredo é escrito toda vez que vem: pôr o segredo de novo é o recadastro.
- **O serviço só sobe com o `agent.json`** (`ConditionPathExists` na unidade). A instalação sem as variáveis
  habilita o serviço, mas não o liga, e diz como instalar de novo. A máquina sem configuração não reinicia em
  laço.
- **Habilita e liga na instalação, reinicia na atualização**, com os trechos que o `dh_installsystemd` do
  debhelper gera, copiados à mão (`deb-systemd-helper` e `deb-systemd-invoke`). Quem desabilitou o serviço
  continua com ele desabilitado depois da atualização. O `prerm` para o serviço só na remoção.
- **O `remove` guarda o `agent.json` e o token;** instalado de novo, o agente volta com o mesmo token. **O
  `purge` apaga `/etc/sentinel-agent` e `/var/lib/sentinel-agent`**, como o Datadog e o Wazuh.

**O que vai dentro:**
- **O agente depende do `dotnet-runtime-10.0` da distribuição**, sem levar o .NET junto. Ele está no arquivo
  do Ubuntu 24.04 e 26.04, nos canais `-updates` e `-security`. Quem corrige o .NET é a atualização de
  segurança do Ubuntu, e não um pacote novo do agente. O pacote tem uns 430 KB.
- **O binário fica em `/usr/lib/sentinel-agent/`**, como o Jellyfin (`/usr/lib/jellyfin`), e a unidade em
  `/usr/lib/systemd/system/`. O `sentinel-agent` é o executável do .NET que acha o runtime pelo
  `/etc/dotnet/install_location`: no `ps` aparece `sentinel-agent`, e não `dotnet`
  ([A6](./escopo.md#a6--nada-escondido-camuflagem-proteção-do-binário-e-driver-de-kernel-saem)).
- **A unidade:** `Type=notify` (o agente avisa quando está pronto), `Restart=on-failure` a cada 30 s,
  `After=network-online.target` e `StateDirectory=sentinel-agent` com `0700`, onde o token mora. Ela não leva
  `ProtectSystem` e afins, como nenhum dos agentes olhados.

**Como se monta:** `./packaging/build-deb.sh` (ou `… arm64`) gera `artifacts/sentinel-agent_<versão>_<arq>.deb`
com o `dotnet publish` e o `dpkg-deb`, sem root e sem nada além do SDK. A versão é a `<Version>` do
`SentinelAgente.csproj`. O pacote traz o `copyright` e o `changelog.gz` que a política do Debian pede.

**Descartado:**
- **Levar o .NET dentro do pacote** (self-contained, como o Jellyfin) ou compilar nativo (Native AOT). Os dois
  funcionam no Debian, que não tem o .NET no arquivo, mas aí cada correção de segurança do .NET pede um pacote
  novo do agente. O pacote passaria de 430 KB para 20 a 70 MB.
- **Um pacote por site com o endereço e o segredo dentro**, como o `fleetctl package --enroll-secret` do Fleet
  e o script gerado do Tactical RMM. Cada site teria de montar o seu, e o segredo da frota ficaria num arquivo
  que circula.
- **O debconf.** Ele perguntaria na tela, o que não serve para instalar em muitas máquinas; o Wazuh tem o
  arquivo de perguntas, mas não o usa.
- **O debhelper** (`dpkg-buildpackage`). Ele gera os mesmos scripts, mas pede a pasta `debian/` inteira e
  pacotes a mais para montar. O PowerShell também monta o `.deb` com o `dpkg-deb` direto
  (`tools/packaging/packaging.psm1`).
- **O nfpm ou o fpm** (Fleet, Telegraf, Alloy). Seriam mais uma ferramenta para instalar, para um pacote com
  quatro arquivos.
- **`ProtectProc=invisible` na unidade.** No .NET 10 ele impede o agente de perceber que roda no systemd, e o
  `Type=notify` nunca recebe o "pronto" (a correção é do .NET 11, `dotnet/runtime#125520`).

**Por quê:** é o que os agentes olhados fazem, cada um com uma parte:

| Peça | De onde vem |
|---|---|
| endereço e segredo em variáveis da instalação, lidos pelo `postinst` | Wazuh (`register_configure_agent.sh`), Datadog (`install_script.sh.template`) |
| a configuração escrita na instalação, e não entregue como conffile | Telegraf (`scripts/deb/post-install.sh`), Datadog |
| o segredo num arquivo só do root, trocado pelo token da máquina | Wazuh 5 (`etc/enrollment_token`), Tactical RMM (`install.go`) |
| o `prerm` que para só na remoção, e a atualização que reinicia | Telegraf (`pre-remove.sh`), Fleet (`deb.go`), debhelper (`dh_installsystemd`) |
| o `purge` que apaga a configuração e o estado | Datadog (`postrm`), Wazuh |
| o runtime do sistema, e o binário em `/usr/lib/<pacote>` | Jellyfin (`jellyfin-packaging/debian`) |
| o `.deb` montado pelo `dpkg-deb` | PowerShell (`packaging.psm1`) |

**O custo, declarado:**
- **O segredo aparece no `ps`** enquanto o `apt` roda, e fica no histórico do shell de quem instalou, como no
  Wazuh e no Datadog. Quem não quer isso escreve os dois arquivos à mão e roda
  `sudo systemctl restart sentinel-agent`.
- **Debian e Fedora ficam de fora.** No Debian, o `dotnet-runtime-10.0` vem do repositório da Microsoft
  (`packages.microsoft.com`), e com ele o mesmo pacote instala. O `.rpm` continua de fora (A3).
- **A licença do repositório não está definida:** o `copyright` diz isso.

---

## A21 — A pasta `Service` sai: a vida do serviço é do `AddSystemd()`

**Decidido** (2026-11-15), corrigindo em parte a
[A2](#a2--um-projeto-com-uma-pasta-por-função): a pasta `Service/` sai. Avisar o systemd que o agente está
pronto, parar quando ele manda e escrever no journal é o `AddSystemd()` do `Program.cs`
([A3](#a3--o-agente-é-um-serviço-do-systemd-instalado-por-pacote)), e o laço de cada função mora na pasta
dela (`Connection/AgentConnection.cs`, `Telemetry/TelemetryService.cs`). Não sobrou nada para a pasta.

**Descartado:** **guardar a pasta vazia** para o dia em que o agente rodar em outro sistema. A A2 a justificava
pelo `orbit/pkg/osservice` do Fleet, e no Linux ele é um corpo vazio (`osservice_notwindows.go`: "a placeholder
for non-windows OSes"); só o `osservice_windows.go` tem código. O Windows está fora (A2), e a pasta volta junto
com ele, se ele vier.

**Por quê:** é o que os projetos olhados fazem no Linux. O Tactical RMM põe a vida do serviço no
`agent/svc.go`, ao lado das outras funções, e não numa pasta à parte; o GLPI Agent, no
`lib/GLPI/Agent/Daemon.pm`, com uma pasta `Daemon/` só para o `Win32.pm`. Nos dois, e no Fleet, o que é
do systemd fica com o systemd.
