# Decisões da plataforma

> A linguagem, as pastas e como o agente chega à máquina.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve**. Se a realidade mudou, a decisão é
> **superada** por outra, com link entre as duas. As supersessões estão em [`README.md`](./README.md).
>
> Decisões neste arquivo: A1–A3.

---

## A1 — O agente recomeça do zero, em C# com .NET 10

**Decidido** (2026-11-13): o agente é reescrito do zero neste repositório, em C# com .NET 10. O repositório
antigo (`pauloeduas-software/SentinelAgent`) fica só como consulta; o que não volta dele está em
[`escopo.md`](./escopo.md).

**Descartado:**
- **Consertar o agente antigo.** Além do que está no escopo, ele divergia do contrato do servidor: o endereço
  fixo em `ws://localhost:5000/agent-hub` (`Program.cs`), a conexão sem `Authorization: Bearer`, o total de
  rede em gigabits num campo `TotalRxGb` e o handshake sem série, UUID, fabricante, modelo, gabinete e
  totais — o que levou o SentinelWeb a tirar as especificações da tela
  ([BY](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/reconciliacao.md#by--a-aba-máquina-mostra-o-que-o-agente-manda)).
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
