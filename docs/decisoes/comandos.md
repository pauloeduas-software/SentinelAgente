# Decisões dos comandos

> O que o agente executa quando o SentinelWeb manda.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve**. Se a realidade mudou, a decisão é
> **superada** por outra, com link entre as duas. As supersessões estão em [`README.md`](./README.md).
>
> Decisões neste arquivo: A5, A18 e A23 (a A5 superada em parte pela A23 e pela A26, e a A23 em parte pela A26).

---

## A5 — Comando é lista fechada, sem shell

> **Superada em parte por [A23](#a23--a-lista-fica-nas-três-ações-e-o-terminal-não-entra):** os comandos de terminal saíram de avaliação e não entram.
>
> **Superada em parte por [A26](./protocolo.md#a26--o-refresh-manda-o-inventário-e-a-telemetria-na-hora-a-pedido-do-painel):** o `REFRESH` chega como `Action` e não é recusado; a conexão o trata
> antes da lista.

**Decidido** (2026-11-13):
- O agente conhece as três ações que o servidor manda: `SHUTDOWN`, `REBOOT` e `SUSPEND`. Outra `Action` é
  recusada e vai para o log, com o `CommandId`.
- Cada ação é um executável fixo, chamado sem shell — argumentos em lista, nunca `sh -c`:

| Action | Executa |
|---|---|
| `SHUTDOWN` | `systemctl poweroff` |
| `REBOOT` | `systemctl reboot` |
| `SUSPEND` | `systemctl suspend` |

- O texto que chega do servidor nunca vira comando: ele só escolhe uma linha da tabela.

**Descartado:** o jeito do antigo (`CommandDispatcher`), que já tinha a lista mas chamava `reboot`,
`shutdown -h now` e `systemctl suspend` com `UseShellExecute = true`, e ignorava em silêncio a ação que não
conhecia. Agora é um caminho só (`systemctl`), sem shell, e a recusa aparece no log.

**Por quê:** o agente roda como root
([A3](./plataforma.md#a3--o-agente-é-um-serviço-do-systemd-instalado-por-pacote)). O servidor já tem a mesma
lista, e o comentário de lá diz o porquê: "Texto livre ia direto ao agente, que roda como root"
(`server/modules/endpoint/schemas/endpoint.schema.ts`). Repetir a lista no agente é o que segura o caso em que
quem fala com ele não é o servidor de verdade.

**O que fica de fora, declarado:**
- **Comandos de terminal.** Dá para fazer, e é a parte perigosa: quem invadir o painel ou o servidor mandaria
  em todas as máquinas como root. A viabilidade está em avaliação; se vier, é decisão própria, com conexão
  criptografada (`wss://`) obrigatória e o registro de quem mandou.
- **A confirmação de execução.** O protocolo não tem resposta do agente; mostrar "executado" na tela começa no
  SentinelWeb, com um tipo de mensagem novo.

---

## A18 — O comando roda o `systemctl` direto, respeita os bloqueios e diz no log como terminou

**Decidido** (2026-11-15), completando a [A5](#a5--comando-é-lista-fechada-sem-shell):
- **O executável é `/usr/bin/systemctl`, pelo caminho completo**, com o verbo da tabela da A5 como único
  argumento, sem shell. O `PATH` não escolhe o programa.
- **O agente dispara e volta.** O `systemctl` pede a ação ao logind e sai assim que ela entra na fila, sem
  esperar a máquina desligar ou suspender. Ele roda fora da recepção, e o fim da conexão não o interrompe: a
  ação já foi pedida. Se ele não voltar em 15 s, é morto, e isso vai para o log.
- **O resultado vai para o log, com o `CommandId`:** aceito pelo systemd, ou recusado com o código de saída e
  o motivo que o `systemctl` escreveu (a máquina que não suspende, a ação que já está em andamento).
- **Os bloqueios do logind valem.** O agente não passa `--check-inhibitors=no`: desde o systemd 257 o root
  também respeita o bloqueio do tipo `block`, e quem o segura (uma atualização de pacotes, um gravador de
  disco) recebe a recusa no log em vez de ter o trabalho cortado no meio. O bloqueio do tipo `delay` só
  atrasa, no máximo `InhibitDelayMaxSec` (5 s por padrão).
- **Ação desconhecida não é normalizada:** `reboot` em minúsculas é recusado como qualquer outro texto. O
  servidor manda sempre em maiúsculas (`endpoint.schema.ts`).

**Descartado:**
- **A chamada ao kernel direto**, como o MeshAgent (`syscall(SYS_reboot, …)` em `meshcore/meshinfo.c`): ela
  pula o systemd, e os serviços não param direito.
- **O shell**, como o agente do Tactical RMM (`/bin/bash -c "shutdown -h now"` em `agent/rpc.go`). Aqui não há
  nada para o shell fazer.
- **O D-Bus do logind direto** (`PowerOffWithFlags`, `CanSuspend`): é a mesma chamada que o `systemctl` faz,
  mais uma dependência. O `systemctl` ainda cai para o `poweroff.target` quando o logind não responde.
- **Passar por cima dos bloqueios** (`-i`). Ele só funciona onde a regra do polkit para o root vem instalada
  (o Ubuntu traz; outras distribuições, só o exemplo), e o que ele corta é justamente o que pediu para não
  ser cortado.
- **Uma chave na configuração para desligar os comandos**, como o `--enable-scripts` do Fleet e o
  `<active-response><disabled>` do Wazuh. Lá, a chave protege da execução de script qualquer; aqui, a lista
  tem três ações, e quem tem permissão de desligar no painel já é Supervisor.
- **Descartar `CommandId` repetido.** O servidor gera um id novo por comando e não reenvia (não há fila), e o
  logind já recusa a segunda ação com a primeira em andamento.

**Por quê:** os agentes olhados confirmam a lista fechada, mas com shell (Tactical RMM) ou sem systemd
(MeshAgent), e nenhum diz no log por que a ação não aconteceu. Como o protocolo não tem resposta do agente
(A5), o log é o único lugar onde a recusa aparece: é ali que quem administra cruza o `CommandId` com a
auditoria do servidor.

**O que fica de fora, declarado:**
- **A confirmação para a tela.** O Tactical RMM responde "ok" antes de executar, e o Fleet agenda o reboot para
  10 s depois (`systemd-run --on-active=10s`, em `linux_lock.sh`) só para dar tempo de mandar o resultado.
  Quando o SentinelWeb ganhar a mensagem de resposta, o agente manda o resultado do `systemctl`, que volta
  antes de o desligamento começar.
- **A conexão depois da suspensão.** A máquina volta com o socket aberto e morto; o ping do WebSocket (30 s,
  com 15 s de espera, em `Connection/ConnectionTimings.cs`) derruba a conexão e o agente reconecta.

---

## A23 — A lista fica nas três ações, e o terminal não entra

> **Superada em parte por [A26](./protocolo.md#a26--o-refresh-manda-o-inventário-e-a-telemetria-na-hora-a-pedido-do-painel):** o pedido de inventário na hora volta, tratado pela conexão e fora da lista dos comandos de sistema.

**Decidido** (2026-11-15), corrigindo em parte a [A5](#a5--comando-é-lista-fechada-sem-shell):
- **O agente fica com `SHUTDOWN`, `REBOOT` e `SUSPEND`**, e a lista não cresce. Outra `Action` continua
  recusada e no log, como na A5.
- **Comandos de terminal não entram.** A A5 os deixava em avaliação; a avaliação acabou.

**Descartado:**
- **O terminal remoto**, como o do Tactical RMM e o do MeshAgent. Quem tomasse o painel ou o servidor
  rodaria qualquer coisa como root em todas as máquinas de uma vez, e nenhum caso de uso pedido precisa disso.
- **Ações novas na lista fechada:** bloquear a tela (`loginctl lock-sessions`), hibernar, pedir o inventário
  na hora, atualizar os pacotes. Cada uma seria um executável fixo e caberia na regra da A5, mas nenhuma foi
  pedida pelo painel, e o inventário já se refaz a cada conexão.

**Por quê:** o agente é de inventário e telemetria; os três comandos de energia são o que o painel usa. Cada
ação nova é mais um jeito de o servidor mandar como root, e sem a mensagem de resposta (fora na A5 e na A18)
o painel nem saberia se ela rodou.

**O que fica de fora, declarado:** a resposta do agente aos três comandos não é comando novo e continua
possível, do jeito que a [A18](#a18--o-comando-roda-o-systemctl-direto-respeita-os-bloqueios-e-diz-no-log-como-terminou)
descreve, quando o SentinelWeb ganhar a mensagem.
