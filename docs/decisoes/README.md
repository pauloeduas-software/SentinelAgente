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
| [`plataforma.md`](./plataforma.md) | **Plataforma** — a linguagem, as pastas e a instalação | A1, A2, A3, A19, A21, A27 |
| [`protocolo.md`](./protocolo.md) | **Protocolo** — o que o agente diz ao SentinelWeb | A4, A9, A10, A11, A12, A13, A14, A15, A16, A20, A22, A24, A25, A26 |
| [`comandos.md`](./comandos.md) | **Comandos** — o que o agente executa | A5, A18, A23 |
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
| **A4**, **A13** e **A15** (em parte: o hash do HWID) | **A20** | o HWID é um UUID sorteado na instalação, e o SentinelWeb reconhece a máquina reinstalada pelo serial e o UUID; o `BiosSerial` cai no serial da placa |
| **A2** (em parte: a pasta `Service`) | **A21** | a pasta sai: a vida do serviço é do `AddSystemd()`, e o laço de cada função fica na pasta dela |
| **A10**, **A16** e **A2** (em parte) | **A22** | os programas e as placas de vídeo vão no handshake, no formato do GLPI, antes de o SentinelWeb guardar; a pasta `Telemetry/Gpu` sai, e o uso da GPU continua fora |
| **A10**, e em parte **A14**, **A16** e **A22** | **A24** | os programas, a RAM do handshake, a rede e os processos saem, e a GPU fica com o nome e a memória: o agente manda só o que o SentinelWeb lê |
| **A14** (em parte: o inventário só a cada conexão) | **A25** | o handshake leva a versão do agente e vai de novo uma vez por dia; o 1008 leva o motivo do servidor para o log |
| **A5** (em parte: os comandos de terminal) | **A23** | a lista fica nas três ações de energia e o terminal remoto não entra; a resposta aos comandos continua possível |
| **A23** e **A25** (em parte: pedir o inventário na hora) | **A26** | o `REFRESH` do painel faz o agente mandar o handshake e uma amostra de telemetria na hora; a conexão o trata, e a lista dos comandos de sistema não muda |
| **A2** (em parte: o Windows) | **A27** | o Windows deixa de ser só "fica de fora" e passa a trabalho futuro declarado; nada muda no código agora |
| **A3** (em parte: a configuração no pacote) | **A19** | o `agent.json` e o segredo saem de variáveis da instalação, e o serviço só liga com o `agent.json` |
| **A13** e **A14** (em parte) | **A15** | o serial de fábrica se compara sem separadores e as listas crescem; a Lenovo usa sempre o `product_version`; o túnel se reconhece pelo tipo da placa |

## Os nomes

**Por que `A` e número, e não `D` nem letra pura:** o SentinelWeb já usa os dois — `D1`…`D164` e depois letras
(`A`, `B`… `CQ`). Com o mesmo formato, "a decisão A" daqui e "a decisão A" de lá teriam o mesmo nome. Letra
seguida de número não existe no SentinelWeb, então o nome sozinho diz de qual repositório a decisão é; o `A` é
de Agente.

**Cada repositório guarda as próprias decisões.** As do SentinelWeb não se citam aqui, e as daqui não se citam
lá. O que um precisa saber do outro mora na referência, como fato e sem decisão: aqui, o
[`protocolo.md`](../referencia/protocolo.md), que diz o que o servidor faz; lá, o
[`agente.md`](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/referencia/agente.md), que diz
o que chega do agente.
