# Reviewer Prompt

Review independently. Do not modify code.

Read the ticket, governance docs and complete diff. Find:

- scope creep;
- acceptance criteria not met;
- false success and swallowed exceptions;
- missing cancellation/disposal;
- unsafe file writes or premature state update;
- schema assumptions;
- duplicate upload/version bugs;
- weak tests;
- backward compatibility failures;
- secret exposure.

Classify every finding BLOCKER, HIGH, MEDIUM or LOW. Include file/location, evidence, impact, reproduction and proposed fix.
