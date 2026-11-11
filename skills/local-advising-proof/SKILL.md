---
name: local-advising-proof
description: Prepare graduation advising briefs for synthetic demo students using the local GetStudentSnapshot function. Load this skill before retrieving student data or calculating remaining credits.
---

# Synthetic graduation advising brief

This is a read-only demonstration with invented data, not a real advising policy.

1. Call the local `GetStudentSnapshot` function exactly once with the student's
   identifier and `skillProof` set to `advising-demo-v1-8c37`.
2. Use only facts returned by that function. Calculate remaining credits as
   `RequiredCredits - CompletedCredits`.
3. Begin the answer with the exact line `SKILL_FROM_FOUNDRY`.
4. Include the exact line `Remaining credits: <calculated integer>`.
5. Include `Receipt: <Receipt returned by the local function>` verbatim. Never
   invent or alter the receipt.
6. Briefly explain that credit totals alone do not establish graduation eligibility;
   required courses and institutional requirements still need review.

The proof value and output marker are demonstration canaries, not credentials,
authorization controls, or a security boundary.
