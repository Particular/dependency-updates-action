# dependency-updates-action
Handle .NET versioning concerns for Particular Software component packages

## How it works

A wrapper action, [update-dependencies.yml](https://github.com/Particular/DependencyUpdatesTest/blob/main/.github/workflows/update-dependencies.yml), is synced across repositories by [RepoStandards](https://github.com/Particular/RepoStandards). 

```mermaid
---
title: Give repos access to the action
---

flowchart TD
    repo-sync@{ shape: process, label: update-dependencies.yml }
    
    r1@{ shape: bow-rect, label: Repo 1 }
    r2@{ shape: bow-rect, label: Repo 2 }

    rellip@{ shape: text, label: ... }

    rn-1@{ shape: bow-rect, label: Repo n - 1 }
    rn@{ shape: bow-rect, label: Repo n }

    repo-sync --> r1
    repo-sync --> r2
    repo-sync --> rellip
    repo-sync --> rn-1
    repo-sync --> rn
```

This allows any synced repo to invoke the [shared dependency updates workflow](https://github.com/Particular/dependency-updates-action/blob/main/.github/workflows/run-dependency-updates.yml).

```mermaid
---
title: Repository runs workflow
---
flowchart LR
    ud-invoked(Repository invokes<br/>update-dependencies.yml)
    rdu-invoked[Shared workflow<br />run-dependency-updates.yml<br/>invoked]
    a-invoked[action.yml]
    u-command[/Sets APP_COMMAND environment variable/]
    build-run[Build/Run DependencyUpdates.csproj]
    read-cmd[\Read APP_COMMAND environment variable\]
    run-cmd["`**AVAILABLE COMMANDS**<br/>
        update`"]

    ud-invoked -- "references" --> rdu-invoked
    rdu-invoked --> u-command  -- "runs the action" --> a-invoked
    a-invoked --> build-run
    build-run --> read-cmd --> run-cmd
    subgraph Run command
        run-cmd
    end
```

```mermaid
---
title: Repository runs workflow
---
sequenceDiagram
    participant R as Target Repository
    participant UD as update-dependencies.yml
    participant RDU as run-dependency-updates.yml
    participant A as action.yml
    participant DU as DependencyUpdates.csproj

    activate R
    R->>UD: User or trigger invokes
    UD->>RDU: References shared workflow on DependencyUpdateAction repo
    RDU->>A: Sets APP_COMMAND environment variable and runs action
    Note left of A: AVAILABLE COMMANDS<br/><br/>update
    A->>DU: Builds and runs with 'dotnet run'
    DU->>DU: Reads APP_COMMAND environment variable and runs command
    DU->>R: Act on target repository (open PRs, etc...)
    deactivate R
```