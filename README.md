# Susu tracker

A console program for one rotating savings group. Every member pays the same amount each week. When the week closes and everyone has paid in full, the whole pot goes to the next member in the order they joined.

## Requirements

[.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run

From the repository root:

```bash
dotnet run --project SusuTrackerCli
```

The prompt is `>`. Type `help` for the command list, and `quit` to leave. The group is stored in `susu-group.json` in the working directory and loaded again on the next run. Delete that file to start over. If the file cannot be read, the program stops and leaves it in place.

## Example

Three members and a weekly contribution of 50.00 GHS. Esi's first payment is short, so week 1 stays open. After the remaining 20.00, Ama receives 150.00 and week 2 opens with Kofi next.

```text
> create 50.00
Created a group with a weekly contribution of 50.00 GHS.
> add Ama
Added Ama.
> add Kofi
Added Kofi.
> add Esi
Added Esi.
> start
Week 1 is open. Ama receives the pot if everyone pays in full.
> pay Ama 1 50
Ama has paid in full for week 1.
> pay Kofi 1 50
Kofi has paid in full for week 1.
> pay Esi 1 30
Esi owes 20.00.
> close
Esi owes 20.00.
> pay Esi 1 20
Esi has paid in full for week 1.
> close
Ama received 150.00.
Week 2 is open. Kofi is next.
```

`pay` is `pay <name> <week> <amount>`. A name may contain spaces: `pay Ama Mensah 1 4.50`.

## Commands

| Command | What it does |
|---|---|
| `create <amount>` | Create the group. The amount is the fixed weekly contribution in GHS. |
| `add <name>` | Add a member. This works only before `start`. |
| `start` | Lock the member list and open week 1. Requires at least two members. |
| `pay <name> <week> <amount>` | Record a payment for the current week. Partial payments are allowed. |
| `close` | Pay the pot to the next member if everyone has paid in full. |
| `report` | Show the open week, who has paid, who still owes, and the payout history. |
| `help` | Show the command list. |
| `quit` | Exit. |

## Tests

From the repository root:

```bash
dotnet test
```

The suite covers group setup, partial payments, a week that cannot close, the payout in the example above, a full rotation, the report, the command loop, and saving the group to disk.
