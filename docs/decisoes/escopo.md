# Decisões do escopo

> O que o agente antigo fazia e este não faz, e por quê.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve**. Se a realidade mudou, a decisão é
> **superada** por outra, com link entre as duas. As supersessões estão em [`README.md`](./README.md).
>
> Decisões neste arquivo: A6–A8 e A17 (a A7 superada pela A9, que a A17 superou, e a A8 em parte pela A10 e pela A24).

---

## A6 — Nada escondido: camuflagem, proteção do binário e driver de kernel saem

**Decidido** (2026-11-13): não voltam do agente antigo o `ProcessCamouflage`, o `BinaryProtection`, o
`LinuxSecurity` e o driver de kernel `AssetManager.Shield` (`scripts/kernel_driver/`). O agente aparece com o
próprio nome no `ps`, no `top` e no `systemctl status`, o binário é um arquivo comum, e
`sudo systemctl stop sentinel-agent` o para.

**Por quê:** um agente que roda como root e recebe ordem pela rede só é aceitável se quem administra a
máquina o enxerga e o controla. Esconder o processo e blindar o binário é o que antivírus e EDR procuram, e é
o que separa um agente de inventário de um malware: com isso de volta, a ferramenta de segurança da empresa o
trataria como ameaça, e com razão. O requisito "só desinstala com sudo" já vem do Linux
([A3](./plataforma.md#a3--o-agente-é-um-serviço-do-systemd-instalado-por-pacote)).

**O que fica de fora, declarado:** proteção contra quem já é root na máquina. Ele para o agente, e é para poder.

---

## A7 — A telemetria não tem buffer offline

> **Superada por [A9](./protocolo.md#a9--o-buffer-offline-volta-com-a-hora-da-coleta):** o buffer volta, e a amostra leva a hora da coleta.

**Decidido** (2026-11-13): sem conexão, a amostra é descartada. O `OfflineBuffer` do antigo, uma fila em
memória que reenviava as amostras na reconexão, não volta.

**Por quê:** o servidor grava a amostra com a hora em que ela chega (`Telemetry.timestamp @default(now())`), e o
protocolo não tem campo de hora. As amostras guardadas durante uma queda chegariam juntas na reconexão e
seriam gravadas como uso daquele instante — o agregado de uso do dia contaria horas numa hora que não as teve.

**O que fica de fora, declarado:** o histórico do tempo sem rede. Se um dia precisar, começa no SentinelWeb,
com a hora da coleta no protocolo; aí o buffer volta a fazer sentido.

---

## A8 — O handshake não manda programas instalados nem usuário logado

> **Superada em parte por [A10](./protocolo.md#a10--a-lista-de-programas-instalados-volta):** a lista de programas volta; o usuário logado continua fora.
>
> **Superada em parte por [A24](./protocolo.md#a24--o-agente-manda-só-o-que-o-sentinelweb-lê-os-programas-a-rede-e-os-processos-saem):** a telemetria de processos, que a A8 dava como o
> lugar de "quais apps estão rodando", também sai.

**Decidido** (2026-11-13): o handshake não leva `InstalledSoftware`, que o antigo mandava, nem
`LoggedOnUser`.

**Por quê:** o servidor aceita os dois e descarta (`normalize-payload.helper.ts`): a conformidade de software
saiu do SentinelWeb, e a reconciliação parou de inferir pessoa pela conta do sistema. A lista de programas era
a parte mais pesada do handshake, e ninguém a lia.

**O que fica de fora, declarado:** "quais apps estão rodando" não é isto: é a telemetria de processos
(`Telemetry/Processes`), que o servidor já recebe.

---

## A17 — Sem buffer offline: a amostra sem conexão é descartada

**Decidido** (2026-11-15): o agente não guarda a amostra que não pôde mandar. Sem conexão, ela é descartada, e a
próxima vai na hora seguinte. O protocolo não ganha o campo de hora da coleta, e o SentinelWeb não muda.

**Supera:** [A9](./protocolo.md#a9--o-buffer-offline-volta-com-a-hora-da-coleta).

**Por quê:**
- **O ganho é pequeno com uma amostra por hora**
  ([A12](./protocolo.md#a12--os-tempos-telemetria-de-hora-em-hora-e-ping-de-30-a-60-s)): uma queda de 3 horas
  perde 3 pontos do gráfico. A queda já aparece como OFFLINE, e a primeira amostra da volta mostra o estado
  atual, que é o que o painel mais usa.
- **O dado recuperado dura pouco:** a amostra crua fica 3 dias no servidor.
- **O custo é dos dois lados:** o campo de hora e a recusa da hora no futuro no SentinelWeb, a fila com
  limite no agente, e o relógio errado da máquina gravando pontos no lugar errado.
- **Quem guarda é quem vive da série:** o Telegraf (`metric_buffer_limit`) é um encanamento de métricas, onde
  cada ponto importa. Aqui a telemetria é o retrato da máquina, de hora em hora.

**Quando volta:** se a telemetria ficar frequente (a cada minuto), se surgir alerta ou relatório que precise da
série completa, ou se a frota tiver muitas máquinas que passam dias fora da rede. Aí volta como a A9 pedia:
começa no SentinelWeb, com a hora da coleta.
