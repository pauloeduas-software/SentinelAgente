# As decisões do SentinelAgente

> Toda decisão de arquitetura do agente, arquivada **pelo assunto que ela governa**, como no SentinelWeb.
> O nome é `A` seguido do número (`A1`, `A2`…) e não muda, mesmo que o arquivo onde a decisão vive mude.
> Para achar uma, procure o título: `grep -rn '^## A4 ' docs/decisoes`.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve.** Se a realidade mudou, ela é
> **superada** por outra, com link entre as duas.
>
> Quem quer saber **o que vale hoje**, leia [`../referencia/`](../referencia/).
> Quem quer saber **o que foi feito e quando**, veja o `git log`.

## Os quatro assuntos

| Arquivo | Assunto | Decisões |
|---|---|---|
| [`plataforma.md`](./plataforma.md) | **Plataforma** — a linguagem, as pastas e a instalação | A1, A2, A3, A19 |
| [`protocolo.md`](./protocolo.md) | **Protocolo** — o que o agente diz ao SentinelWeb | A4, A9, A10, A11, A12, A13, A14, A15, A16, A20 |
| [`comandos.md`](./comandos.md) | **Comandos** — o que o agente executa | A5, A18 |
| [`escopo.md`](./escopo.md) | **Escopo** — o que o agente antigo fazia e este não faz | A6, A7, A8, A17 |

## Correções e supersessões

As decisões não se reescrevem, então quando uma corrige a outra isso fica escrito nas duas:

| Corrige | É corrigida por | O que mudou |
|---|---|---|
| **A7** (sem buffer offline) | **A9** | o buffer volta, e toda amostra leva a hora da coleta; depende do SentinelWeb ler essa hora |
| **A9** | **A17** | o buffer sai de novo: com uma amostra por hora, não vale o custo; a amostra sem conexão é descartada |
| **A8** (em parte: os programas instalados) | **A10** | a lista de programas volta, pelo dpkg ou rpm; depende do SentinelWeb guardar e mostrar |
| **A11** (em parte: o `Ping`) | **A12** | o `Ping` vai a cada 30 a 60 s, e o servidor põe OFFLINE após 2 min; a telemetria é de hora em hora |
| **A4** (em parte: os genéricos e a máquina sem serial) | **A13** | a lista de seriais genéricos é a do SentinelWeb, e o UUID do firmware entra quando os dois seriais são genéricos |
| **A4**, **A13** e **A15** (em parte: o hash do HWID) | **A20** | o HWID é um UUID sorteado na instalação, e o SentinelWeb reconhece a máquina reinstalada pelo serial e o UUID (a CV de lá); o `BiosSerial` cai no serial da placa |
| **A3** (em parte: a configuração no pacote) | **A19** | o `agent.json` e o segredo saem de variáveis da instalação, e o serviço só liga com o `agent.json` |
| **A13** e **A14** (em parte) | **A15** | o serial de fábrica se compara sem separadores e as listas crescem; a Lenovo usa sempre o `product_version`; o túnel se reconhece pelo tipo da placa |

## Os nomes

**Por que `A` e número, e não `D` nem letra pura:** o SentinelWeb já usa os dois — `D1`…`D164` e depois letras
(`A`, `B`… `CQ`) — e este repositório cita as decisões de lá o tempo todo (o `H`, o `D46`, o `BY`). Com o
mesmo formato, "a decisão A" daqui e "a decisão A" de lá teriam o mesmo nome. Letra seguida de número não
existe no SentinelWeb, então o nome sozinho diz de qual repositório a decisão é; o `A` é de Agente. Decisão do
SentinelWeb citada aqui leva sempre o link para lá.
