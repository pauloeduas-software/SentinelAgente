# Instalar o agente

> Guia. O *porquê* está na
> [A19](../decisoes/plataforma.md#a19--o-deb-runtime-do-ubuntu-endereço-e-segredo-nas-variáveis-da-instalação).

---

## Montar o pacote

Na máquina de desenvolvimento, sem root, num clone do repositório: o script usa o SDK, o `dpkg-deb` e a data
do último commit.

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

O segredo é o `AGENT_ENROLL_SECRET` do `.env` do SentinelWeb, que quem administra o servidor gera
(`openssl rand -hex 32`); nenhuma tela o mostra. O agente o troca pelo token da máquina no primeiro contato e
apaga o arquivo. Confira:

```bash
systemctl status sentinel-agent
journalctl -u sentinel-agent -f
```

Sem o `SENTINEL_URL`, o pacote instala, avisa que o serviço não sobe sem o `agent.json` e deixa a unidade
parada. Com o endereço e sem o segredo, o serviço não chega a subir: diz no journal que falta o segredo, e o systemd
tenta de novo a cada 30 s.

**Sem o segredo no `ps`:** instale sem as variáveis (o aviso de que falta o `agent.json` é esperado aqui),
escreva os arquivos à mão e ligue:

```bash
sudo apt install ./sentinel-agent_<versão>_amd64.deb
echo '{ "Server": { "Url": "wss://<servidor>/agent-hub" } }' | sudo tee /etc/sentinel-agent/agent.json
sudo install -m 600 /dev/null /etc/sentinel-agent/enroll-secret
sudo nano /etc/sentinel-agent/enroll-secret
sudo systemctl restart sentinel-agent
```

**Servidor com certificado interno:** copie a CA (PEM) para `/etc/sentinel-agent/ca.pem`, acrescente
`"CaFile": "/etc/sentinel-agent/ca.pem"` no `Server` do `agent.json` e rode
`sudo systemctl restart sentinel-agent`. Sem o arquivo, o agente não sobe.

## Atualizar

```bash
sudo apt install ./sentinel-agent_<versão nova>_amd64.deb
```

O `agent.json`, o token e o HWID ficam como estavam, e o serviço reinicia. O `SENTINEL_URL` só vale enquanto
não há `agent.json`: com ele no lugar, é ignorado, com um aviso. Para trocar o endereço, edite
`/etc/sentinel-agent/agent.json` e rode `sudo systemctl restart sentinel-agent`.

## Recadastrar

Depois de o servidor recusar o token (401) ou fechar a conexão com 1008 (HWID diferente do token, ou máquina
excluída), ponha o segredo de novo. O agente procura o segredo antes de cada tentativa, que vem em até 5 min
(ou na hora, com `sudo systemctl restart sentinel-agent`). Basta gravá-lo em
`/etc/sentinel-agent/enroll-secret`, com `chmod 600`, como no caminho sem o segredo no `ps`, ou reinstalar:

```bash
sudo SENTINEL_ENROLL_SECRET=<segredo> apt install --reinstall ./sentinel-agent_<versão>_amd64.deb
```

Se o 401 for do segredo de instalação ("O servidor recusou o segredo de instalação"), o segredo está errado:
confira contra o `AGENT_ENROLL_SECRET` do SentinelWeb.

## Remover

```bash
sudo apt remove sentinel-agent    # para o serviço; guarda o agent.json, o token e o HWID
sudo apt purge sentinel-agent     # apaga também /etc/sentinel-agent e /var/lib/sentinel-agent
```

Instalado de novo depois do `remove`, o agente volta como a mesma máquina. Depois do `purge`, é como a
primeira vez: a instalação precisa do endereço e do segredo, e o agente sorteia outro HWID ao subir. No
SentinelWeb, a máquina volta na mesma linha, com o computador vinculado, quando a série dela é legível e só
ela tem essa série.
