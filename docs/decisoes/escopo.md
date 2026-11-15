# Decisões do escopo

> O que o agente antigo fazia e este não faz, e por quê.
>
> **A regra desta pasta:** decisão escrita aqui **não se reescreve**. Se a realidade mudou, a decisão é
> **superada** por outra, com link entre as duas. As supersessões estão em [`README.md`](./README.md).
>
> Decisões neste arquivo: A6–A8 (a A7 superada pela A9, e a A8 em parte pela A10).

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

**Decidido** (2026-11-13): o handshake não leva `InstalledSoftware`, que o antigo mandava, nem
`LoggedOnUser`.

**Por quê:** o servidor aceita os dois e descarta (`normalize-payload.helper.ts`): a conformidade de software
saiu do SentinelWeb (decisão G, em
[`licencas.md`](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/licencas.md)), e a
reconciliação parou de inferir pessoa pela conta do sistema (decisão A, em
[`reconciliacao.md`](https://github.com/pauloeduas-software/SentinelWeb/blob/main/docs/decisoes/reconciliacao.md)).
A lista de programas era a parte mais pesada do handshake, e ninguém a lia.

**O que fica de fora, declarado:** "quais apps estão rodando" não é isto: é a telemetria de processos
(`Telemetry/Processes`), que o servidor já recebe.
