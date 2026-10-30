# Northwind demo hosting

The public Northwind demo site runs on Render's free tier as one Docker web service. It is
`Polhem.Northwind.Blazor` in its Demo environment, built from
[`apps/Polhem.Northwind/Dockerfile`](../apps/Polhem.Northwind/Dockerfile). What the Demo environment does is described
in [the Northwind README](../apps/Polhem.Northwind/README.md#both-web-clients-in-one-site-the-demo-environment), and
the reasons are in the source it points to.

The service's settings live in Render's dashboard, not in the repository. This page records them so the service can be
recreated.

## Render service settings

| Setting | Value | Why |
|---------|-------|-----|
| Type / language | Web Service, Docker | |
| Repository / branch | `polhem-dev/polhem`, `main` | |
| Dockerfile path | `apps/Polhem.Northwind/Dockerfile` | |
| Docker build context | `.` (the repository root) | the projects reference `src/` through `ProjectReference` |
| Region | Singapore | Blazor Server makes a round trip per interaction, and most readers are in Taiwan; the region cannot be changed later |
| Instance type | Free | |
| Health check path | `/` | |
| Auto-Deploy | Off | `main` changes many times a day and each image build spends the free workspace's build minutes |
| Environment variables | none | the image sets `ASPNETCORE_ENVIRONMENT` and reads `PORT` from Render; the Demo environment creates a random `POLHEM_MASTER_KEY` at start when none is set |

No disk is attached. Render's free file system is ephemeral, which is what resets the demo: every new container
seeds `northwind.db` again. It is also why a master key that changes on every start costs nothing: the sessions it
protects go with the database. Attaching a disk would keep the sessions and lose their key, and visitors would have to
sign in again after every restart.

## Client addresses

The rate limits read the client from `CF-Connecting-IP`, accepted only from a loopback connection, which is where
Render's proxy connects from. `NorthwindDemo.AddForwardedClientAddress` records what was measured. This relies on two
things about Render that nothing checks:

- **Cloudflare stays in front.** Cloudflare is what overwrites the header and refuses a supplied one. Without it a client
  could choose its own rate-limit partition, and nothing would show it; the header could also simply go missing, and
  every visitor would then share one limit.
- **The proxy connects from loopback.** If it stops doing so the header is ignored, every visitor shares the proxy's
  address, and the site answers 429 under modest traffic.

To recheck, send more than 60 `/api` requests from one machine: the rest should get 429, while a request from another
network still gets through.

## Deploying

Run the **Deploy Northwind Demo** workflow
([`.github/workflows/northwind-demo-deploy.yml`](../.github/workflows/northwind-demo-deploy.yml)) from the Actions
tab. It calls the service's deploy hook, and Render builds the latest commit of `main`.

The hook URL is the repository secret `RENDER_NORTHWIND_DEPLOY_HOOK`. Render shows it under the service's
**Settings > Deploy Hook**. The URL alone is enough to trigger a deploy, so regenerate it there and update the secret if
it leaks.

## Free tier behaviour to expect

The terms are Render's and change; <https://render.com/docs/free> is the reference. As of the setup:

- The service spins down after 15 minutes without inbound traffic, and the next request waits about a minute while it
  starts. Messages on an open WebSocket count as traffic, so an open Blazor page keeps it awake.
- A spin-down, a restart or a deploy starts a new container, so the data returns to the seed.
- The workspace has a monthly allowance of free instance hours and of build minutes. Without a payment method, builds
  stop for the rest of the month once the build minutes run out; the running service keeps serving the last build.
