# Decisões dos comandos

> O que o agente executa quando o SentinelWeb manda.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve**. Se a realidade mudou, a decisão é
> **superada** por outra, com link entre as duas. As supersessões estão em [`README.md`](./README.md).
>
> Decisões neste arquivo: A5.

---

## A5 — Comando é lista fechada, sem shell

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
