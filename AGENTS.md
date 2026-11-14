# Agent Guidelines

## Code

- Only make changes required by the task, including formatting changes.
- After final edits, check every modified code file using available static analysis tools. Remove unused code and imports, even if pre-existing, and recheck after cleanup. If automated analysis is unavailable, review manually and report that limitation.
- Review every introduced code inspection warning. Apply appropriate fixes; otherwise, explain the tradeoff and ask the human. Never silently ignore or suppress warnings.
- Use the null-forgiving operator (`!`) only with explicit human approval and when null is provably impossible but the compiler cannot establish it. Explain that proof when requesting approval.
- Aim to create small methods with meaningful names.
- Order methods by call flow: callers before callees, with callees in the order they are called. For interface implementations, follow the interface's declaration order instead.
- Keep corresponding parameters in a consistent order across related method signatures and calls.
- Prefer polymorphism or explicit interface contracts over branching on runtime types when the design allows it.

## Tests

- Place test setup and constants in a `Setup` region at the top of the class, and test utility methods in a `Utility Methods` region at the bottom.
- Prefer `[Fact]`. Use `[Theory]` only when parameterization is needed.
- Keep all unit tests for a class in a single test class. Do not create separate test classes for individual methods.
- Keep test cases to the minimum needed to cover distinct execution paths. Do not add filler or redundant cases.
- Test one behaviour per test. `And` in a test name usually signals combined behaviours; simplify the test, not just its name. Multiple assertions are fine when they verify the same behaviour.
- Set up the current state explicitly instead of calling the method under test to create it. Call the method once per test unless repeated calls are themselves the behaviour being tested. Do not clear mock invocations to hide setup calls.
- Keep test names, scenarios, and data within the class's own domain and responsibilities, without introducing unrelated application concepts.
- Use simple synthetic values for test constants unless the behaviour under test requires real application values or naming conventions.
- Verify call order through mock sequences, not recorded strings.
- NEVER USE CONDITIONALS IN TESTS. This includes `if`/`else`, `switch`, and ternary expressions in test methods, callbacks, and test helpers. Use separate, explicit tests when scenarios require different setup or assertions.

## Version Control

- Keep commits focused on one logical change.
- Follow [Conventional Commits](https://www.conventionalcommits.org/), using imperative subjects under 50 characters.
- Keep commit messages concise. Include a body only when useful to explain why, significant trade-offs, or breaking changes. Wrap at 72 characters.
- Describe changes and rationale, not development activities. Omit test execution summaries, counts, and routine validation details.
- Never invent information.
