# Instalar o agente

> Guia. O *porquê* está na
> [A19](../decisoes/plataforma.md#a19--o-deb-runtime-do-ubuntu-endereço-e-segredo-nas-variáveis-da-instalação).

---

## Montar o pacote

Na máquina de desenvolvimento, sem root:

```bash
./packaging/build-deb.sh          # artifacts/sentinel-agent_<versão>_amd64.deb
./packaging/build-deb.sh arm64    # para ARM
```

A versão é a `<Version>` do `SentinelAgente/SentinelAgente.csproj`.

## Instalar

No Ubuntu 24.04 ou mais novo. O `apt` traz o `dotnet-runtime-10.0` junto:

```bash
sudo SENTINEL_URL=wss://<servidor>/agent-hub SENTINEL_ENROLL_SECRET=<segredo> \
  apt install ./sentinel-agent_<versão>_amd64.deb
```

O segredo é o `AGENT_ENROLL_SECRET` do SentinelWeb. O agente o troca pelo token da máquina no primeiro contato
e apaga o arquivo. Confira:

```bash
systemctl status sentinel-agent
journalctl -u sentinel-agent -f
```

**Sem o segredo no `ps`:** instale sem as variáveis, escreva os arquivos à mão e ligue:

```bash
sudo apt install ./sentinel-agent_<versão>_amd64.deb
echo '{ "Server": { "Url": "wss://<servidor>/agent-hub" } }' | sudo tee /etc/sentinel-agent/agent.json
sudo install -m 600 /dev/null /etc/sentinel-agent/enroll-secret
sudo nano /etc/sentinel-agent/enroll-secret
sudo systemctl restart sentinel-agent
```

**Servidor com certificado interno:** acrescente `"CaFile": "/etc/sentinel-agent/ca.pem"` no `Server` do
`agent.json` e reinicie.

## Atualizar

```bash
sudo apt install ./sentinel-agent_<versão nova>_amd64.deb
```

O `agent.json` e o token ficam como estavam, e o serviço reinicia.

## Recadastrar

Depois de um 401 ou de um 1008 no log, ponha o segredo de novo:

```bash
sudo SENTINEL_ENROLL_SECRET=<segredo> apt install --reinstall ./sentinel-agent_<versão>_amd64.deb
```

## Remover

```bash
sudo apt remove sentinel-agent    # para o serviço; guarda o agent.json e o token
sudo apt purge sentinel-agent     # apaga também /etc/sentinel-agent e /var/lib/sentinel-agent
```
