# Documentação do SentinelAgente

> **O critério das pastas é o ciclo de vida, não o assunto** — o mesmo do SentinelWeb. Cada zona tem uma
> regra, e é a regra que impede a pasta de apodrecer. Antes de escrever em `docs/`, pergunte qual das três
> frases descreve o que você está escrevendo.

| Pasta | A regra | O que mora lá |
|---|---|---|
| [`referencia/`](./referencia/) | **É verdade AGORA.** Mudou o código, muda aqui. Não conta história, não dá instrução. | o que o agente é: as pastas e o que cada uma guarda, e o protocolo com o SentinelWeb |
| [`guias/`](./guias/) | **Começa com um verbo.** Receita para quem já sabe o que quer. | preparar o ambiente, instalar o agente |
| [`decisoes/`](./decisoes/) | **Não se reescreve.** Se a realidade mudou, a decisão é *superada* por outra, com link entre as duas. | as decisões numeradas (`A1`…`A28`), um arquivo por assunto |

E um arquivo na raiz: este mapa.

---

## Por onde entrar

**Sou um agente de código.** O [`CLAUDE.md`](../CLAUDE.md) na raiz é o mapa curto: comandos, o que não se
negocia e o que já foi decidido que não volta.

**Vou mexer no código.** Comece em [`referencia/arquitetura.md`](./referencia/arquitetura.md) — uma pasta por
função, o que cada uma guarda e quem pode usar quem.

**Vou mexer no que o agente manda ou recebe.** [`referencia/protocolo.md`](./referencia/protocolo.md). O
protocolo é do SentinelWeb: a página diz o que o servidor faz hoje, e a fonte é a referência de lá.
Campo, mensagem ou comando novo começa no servidor.

**Vou montar a máquina para trabalhar.** [`guias/preparar-o-ambiente.md`](./guias/preparar-o-ambiente.md).

**Vou instalar o agente numa máquina.** [`guias/instalar-o-agente.md`](./guias/instalar-o-agente.md).

**Quero saber por que isso é assim.** [`decisoes/README.md`](./decisoes/README.md) — os assuntos e os nomes.

**Quero saber o que foi feito e quando.** O `git log`.

---

## Escrevendo aqui

1. **Decida a zona pela regra**, não pelo assunto. "Como fazer X" nunca vai em `referencia/`.
2. **Auditoria e revisão não ganham arquivo.** Elas atualizam o documento que revisaram.
3. **Decisão nova continua a numeração** (a próxima é a `A29`) e entra no arquivo do assunto que ela governa.
   Se ela corrige uma anterior, a antiga ganha `> **Superada por…**` logo abaixo do título, e a tabela de
   supersessões do [`decisoes/README.md`](./decisoes/README.md) cresce. A linha "Decisões neste arquivo" do
   topo e o índice do `decisoes/README.md` também mudam, e a faixa (`A1`…) da tabela acima e o número da
   próxima, aqui e no [`CLAUDE.md`](../CLAUDE.md).
4. **Um fato, um lugar.** Se precisar repetir, cite.
5. **A decisão do SentinelWeb fica no SentinelWeb.** Ela não se cita aqui, e a daqui não se cita lá. O que o
   agente precisa saber do servidor vai em [`referencia/protocolo.md`](./referencia/protocolo.md), como o que
   o servidor faz hoje, e a fonte é a referência de lá
   ([`agente.md`](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/referencia/agente.md)).
