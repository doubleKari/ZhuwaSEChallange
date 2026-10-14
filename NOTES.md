# Notes

## Assumptions

These are the rules the challenge left open. The program enforces each one.

1. There is one group, stored in `susu-group.json` in the working directory. `create` is refused when a group already exists. The weekly amount is fixed at creation.
2. A member is identified by name. The stored spelling is the first one entered, with surrounding spaces removed. A later name that differs only by case is a duplicate. A name may contain spaces.
3. The group is `forming` after `create`, `active` after `start`, and `completed` after the last payout.
4. `start` needs at least two members. After that, the member list and the payout order are locked. Payout order is the order members joined.
5. A payment names a week, and only the current week is accepted. A member may pay that week in several parts. Each amount has to be greater than zero, with at most two decimal places (pesewas). A payment larger than the remainder for that week is refused.
6. Money is `decimal`, so values such as 0.10 and 0.20 stay exact. Amounts are shown with two decimal places.
7. Closing a week is a separate step. If anyone still owes, the program lists each debt in join order, pays nothing, and stays on the same week. The member who will receive the pot also pays that week. The pot is the weekly amount multiplied by the number of members.
8. Week 1 pays the first member, week 2 the second, and so on. After every member has received the pot once, the group is completed. Further payments, new members, and another close are refused. The report still shows the history.
9. A file that is missing means there is no group yet. A file that is unreadable or breaks these rules stops the program, so the next `create` cannot replace it by accident.
10. The program does not sign people in, keep wallets, charge penalties, or run a daily or monthly cycle. Those can wait.

## Decisions

The rules live in `SusuGroup`, which does not read the console or the disk. The command loop prints results and writes the file. Tests call the group, the command loop, and the store directly.

A broken rule throws `SusuException`. Closing a week that is still short returns the list of debts and leaves the group unchanged, because that is a normal result rather than a crash.

The command loop is a prompt. The same rules can be driven without a person sitting at the keyboard, which is how the command tests run.

## With more time

- Open a second rotation with the same members after the first one finishes.
- Keep more than one group.
- Separate the person who records cash from the members who only need to see the week and their own balance.
- Put a deadline on a week, and decide what happens when it passes still short.
- Give each member a balance, so a payment has to come from funds the program already holds.
- Allow a name to be corrected while the group is still forming.
- Stop two sessions from writing the same `susu-group.json` at once.
