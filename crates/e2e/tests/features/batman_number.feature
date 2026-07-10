Feature: Batman Number
  The MVP acceptance scenario (see /docs/MVP.md). Written before the code
  that makes it pass, on purpose — outside-in BDD. It's expected to fail
  (via todo!() panics) until the MVP tickets it depends on are done.

  Scenario: Jim Hammond and Jeff the Shark are connected
    Given a Character "Jim Hammond"
    And a Character "Jeff the Shark"
    And they are connected in the graph
    When I request the Batman Number between "Jim Hammond" and "Jeff the Shark"
    Then I should receive a path connecting them
    And the Batman Number should be greater than zero
