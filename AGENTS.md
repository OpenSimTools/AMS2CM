# Agent Guidelines

## Code

- Only make changes required by the task, including formatting changes.
- Before completing a task, remove unused code (including `using` directives) from all modified files, even if it predates the task.
- Review every introduced code inspection warning. Apply appropriate fixes; otherwise, explain the tradeoff and ask the human. Never silently ignore or suppress warnings.
- Use the null-forgiving operator (`!`) only with explicit human approval and when null is provably impossible but the compiler cannot establish it. Explain that proof when requesting approval.
- Aim to create small methods with meaningful names.
- Order methods by call flow: callers before callees, with callees in the order they are called. For interface implementations, follow the interface's declaration order instead.
- Keep corresponding parameters in a consistent order across related method signatures and calls.
- Prefer polymorphism or explicit interface contracts over branching on runtime types when the design allows it.

## Tests

- Place test setup and constants in a `Setup` region at the top of the class, and test utility methods in a `Utility Methods` region at the bottom.
- Prefer `[Fact]`. Use `[Theory]` only when parameterization is needed.
- Keep test cases to the minimum needed to cover distinct execution paths. Do not add filler or redundant cases.
- Test one behaviour per test. `And` in a test name usually signals combined behaviours; simplify the test, not just its name. Multiple assertions are fine when they verify the same behaviour.
- Keep test names, scenarios, and data within the class's own domain and responsibilities, without introducing unrelated application concepts.
- Verify call order through mock sequences, not recorded strings.
- NEVER USE CONDITIONALS IN TESTS. This includes `if`/`else`, `switch`, and ternary expressions in test methods, callbacks, and test helpers. Use separate, explicit tests when scenarios require different setup or assertions.

## Version Control

- Write your commit message in the imperative: "Fix bug" and not "Fixed bug" or "Fixes bug."
- First line of commit message must be 50 characters or less, then a blank line.
- Remaining text should be wrapped at 72 characters. Prefer bullet points.
