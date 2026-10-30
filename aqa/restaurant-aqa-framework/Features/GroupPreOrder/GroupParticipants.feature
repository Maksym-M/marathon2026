@group-preorder @L1
Feature: Group pre-order participants

  The owner of a booking for several people shares an invite link. Guests join it
  under their name, build their own cart and submit it. The owner sees who joined,
  what each of them ordered and what it comes to.

  The owner is a participant too, always listed first with their own pre-order, so
  a group nobody has joined lists the owner alone, with nothing ordered. The service
  adds up every cart; the page's grand total counts only the submitted carts, which
  are the ones that will be sent to the restaurant.

  Background:
    Given the restaurant serves:
      | dish  | price |
      | Soup  | 10.00 |
      | Steak | 25.00 |
      | Salad | 7.50  |
    And a customer has booked a table for 4

  Rule: The owner sees every participant with their dishes and totals

    @api @smoke
    Scenario: The owner sees each guest's dishes, quantities and total
      Given Anna has joined the group and submitted:
        | dish  | quantity |
        | Soup  | 2        |
        | Salad | 1        |
      And Ben has joined the group and submitted:
        | dish  | quantity |
        | Steak | 1        |
      When the owner looks at the group's participants
      Then the participants are:
        | participant | dishes              | total | status    |
        | owner       | none                | 0     | submitted |
        | Anna        | Salad × 1, Soup × 2 | 27.50 | submitted |
        | Ben         | Steak × 1           | 25.00 | submitted |
      And the total of all carts is 52.50

    @api
    Scenario: The total follows a guest's changes to their cart
      Given Anna has joined the group and added:
        | dish | quantity |
        | Soup | 1        |
      When Anna adds 1 Steak to their cart
      And the owner looks at the group's participants
      Then the total of all carts is 35.00

    @ui @smoke
    Scenario: The owner's page shows what each guest ordered
      Given Anna has joined the group and submitted:
        | dish  | quantity |
        | Soup  | 2        |
        | Salad | 1        |
      And Ben has joined the group and submitted:
        | dish  | quantity |
        | Steak | 1        |
      When the owner opens the group pre-order
      Then the participants are:
        | participant | dishes              | total |
        | owner       | none                | -     |
        | Anna        | Salad × 1, Soup × 2 | 27.50 |
        | Ben         | Steak × 1           | 25.00 |
      And the grand total shown is 52.50

    @ui
    Scenario: The page's grand total grows when a guest submits their cart
      Given Anna has joined the group and submitted:
        | dish | quantity |
        | Soup | 2        |
      And Ben has joined the group and added:
        | dish  | quantity |
        | Steak | 1        |
      When the owner opens the group pre-order
      Then the grand total shown is 20.00
      When Ben submits their cart
      And the owner refreshes the group pre-order
      Then the grand total shown becomes 45.00

  Rule: Guests who have not submitted, or ordered nothing, are still listed

    @api
    Scenario: A guest who has not submitted yet is listed as not submitted
      Given Anna has joined the group and added:
        | dish | quantity |
        | Soup | 1        |
      When the owner looks at the group's participants
      Then the participants are:
        | participant | dishes   | total | status        |
        | owner       | none     | 0     | submitted     |
        | Anna        | Soup × 1 | 10.00 | not submitted |

    @api
    Scenario: A guest who joined without ordering anything is listed
      Given Anna has joined the group
      When the owner looks at the group's participants
      Then the participants are:
        | participant | dishes | total | status        |
        | owner       | none   | 0     | submitted     |
        | Anna        | none   | 0     | not submitted |

    @api
    Scenario: A group nobody has joined lists only the owner
      When the owner looks at the group's participants
      Then only the owner is listed, with nothing ordered

    @ui
    Scenario: The page marks guests who have not submitted, or ordered nothing
      Given Anna has joined the group and added:
        | dish | quantity |
        | Soup | 1        |
      And Ben has joined the group
      When the owner opens the group pre-order
      Then the participants are:
        | participant | dishes | status        |
        | owner       | -      | -             |
        | Anna        | none   | not submitted |
        | Ben         | none   | not submitted |

    @ui
    Scenario: The page of a group nobody has joined shows only the owner and free seats
      When the owner opens the group pre-order
      Then only the owner is listed, with nothing ordered
      And 3 seats are still free

  Rule: Only the owner can see the participants

    @api @negative
    Scenario Outline: Someone other than the owner cannot see the participants
      Given Anna has joined the group
      When <someone> looks at the group's participants
      Then <outcome>

      Examples:
        | someone                        | outcome                        |
        | another customer               | the group appears not to exist |
        | the waiter                     | the group appears not to exist |
        | a guest of the group           | they are asked to sign in      |
        | a visitor who is not signed in | they are asked to sign in      |

    @api @negative
    Scenario: The owner looks for a group that does not exist
      When the owner looks at the participants of a group that does not exist
      Then the group appears not to exist

    @ui @negative
    Scenario: Another customer cannot open the owner's group pre-order
      When another customer opens the owner's group pre-order
      Then another customer is told they have no access
