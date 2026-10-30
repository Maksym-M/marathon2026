@group-preorder @L2
Feature: Submitting the group pre-order

  When the guests have ordered, the owner sends one consolidated pre-order to the
  restaurant. Only the carts the guests submitted are taken. The same dish ordered by
  several people, the owner included, becomes one line with the total quantity. The
  restaurant's order is then marked as submitted by the customer, and the group
  pre-order is closed for changes.

  Background:
    Given the restaurant serves:
      | dish  | price |
      | Soup  | 10.00 |
      | Steak | 25.00 |
      | Salad | 7.50  |
    And a customer has booked a table for 4

  Rule: The restaurant receives every submitted cart, merged by dish

    @api @smoke
    Scenario: Same dishes are merged and carts that were not submitted are left out
      Given the owner has pre-ordered:
        | dish | quantity |
        | Soup | 1        |
      And Anna has joined the group and submitted:
        | dish | quantity |
        | Soup | 2        |
      And Ben has joined the group and submitted:
        | dish  | quantity |
        | Soup  | 1        |
        | Steak | 1        |
      And Cara has joined the group and added:
        | dish  | quantity |
        | Salad | 1        |
      When the owner submits the group pre-order
      Then the restaurant receives the pre-order:
        | dish  | quantity |
        | Soup  | 4        |
        | Steak | 1        |
      And the group pre-order is marked as submitted

    @api
    Scenario: The waiter sees the pre-order the owner submitted
      Given the owner has pre-ordered:
        | dish | quantity |
        | Soup | 1        |
      And Anna has joined the group and submitted:
        | dish  | quantity |
        | Steak | 2        |
      When the owner submits the group pre-order
      Then the waiter sees the pre-order:
        | dish  | quantity |
        | Soup  | 1        |
        | Steak | 2        |

    @ui @smoke
    Scenario: The owner submits on the page and sees what the restaurant receives
      Given the owner has pre-ordered:
        | dish | quantity |
        | Soup | 1        |
      And Anna has joined the group and submitted:
        | dish | quantity |
        | Soup | 2        |
      And Ben has joined the group and submitted:
        | dish  | quantity |
        | Soup  | 1        |
        | Steak | 1        |
      And Cara has joined the group and added:
        | dish  | quantity |
        | Salad | 1        |
      When the owner opens the group pre-order
      And the owner submits the group pre-order
      Then the owner sees that the pre-order was sent to the restaurant with:
        | dish  | quantity |
        | Soup  | 4        |
        | Steak | 1        |

    @ui
    Scenario: The waiter sees the submitted pre-order on their dashboard
      Given the owner has pre-ordered:
        | dish | quantity |
        | Soup | 1        |
      And Anna has joined the group and submitted:
        | dish  | quantity |
        | Steak | 2        |
      And the owner has submitted the group pre-order
      When the waiter opens the reservation's pre-order
      Then the waiter sees the pre-order:
        | dish  | quantity |
        | Soup  | 1        |
        | Steak | 2        |

  Rule: There must be something to send

    @api @negative
    Scenario: Nobody has submitted a cart
      Given the owner has started a pre-order and taken everything out of it again
      And Anna has joined the group and added:
        | dish | quantity |
        | Soup | 1        |
      When the owner submits the group pre-order
      Then the owner is told "There is nothing to submit: neither you nor your guests have added any dishes."

    @ui @negative
    Scenario: The page does not let the owner submit while nothing can be sent
      Given Anna has joined the group and added:
        | dish | quantity |
        | Soup | 1        |
      When the owner opens the group pre-order
      Then the owner cannot submit the pre-order yet

    # A submitted cart can't be empty: a guest can't submit one, and a cart the owner
    # empties goes back to "not submitted". Both ways lead to "nothing to submit".
    @api @negative
    Scenario: A guest cannot submit an empty cart
      Given Anna has joined the group
      When Anna tries to submit their empty cart
      Then Anna is told "The cart is empty"

    @api @negative
    Scenario: The only submitted cart was emptied by the owner
      Given the owner has started a pre-order and taken everything out of it again
      And Anna has joined the group and submitted:
        | dish | quantity |
        | Soup | 1        |
      And the owner has taken the Soup out of Anna's cart
      When the owner submits the group pre-order
      Then the owner is told "There is nothing to submit: neither you nor your guests have added any dishes."

    @api @negative
    Scenario: The owner never started a pre-order of their own
      Given Anna has joined the group and submitted:
        | dish | quantity |
        | Soup | 1        |
      When the owner submits the group pre-order
      Then the owner is told their own pre-order could not be found

  Rule: Only an existing group of the owner's own reservation can be submitted, once

    Background:
      Given the owner has pre-ordered:
        | dish | quantity |
        | Soup | 1        |
      And Anna has joined the group and submitted:
        | dish  | quantity |
        | Steak | 1        |

    @api @negative
    Scenario: The reservation does not exist
      When the owner submits the group pre-order for a reservation that does not exist
      Then the reservation appears not to exist

    @api @negative
    Scenario: The group does not exist
      When the owner submits the pre-order of a group that does not exist
      Then the group appears not to exist

    @api @negative
    Scenario: The group belongs to another reservation
      Given the owner has another booking, for 3
      When the owner submits the group pre-order with the reservation of the other booking
      Then the owner is told "Group reservation does not belong to this reservation."

    @api @negative
    Scenario: A group that was already submitted cannot be submitted again
      Given the owner has submitted the group pre-order
      When the owner submits the group pre-order
      Then the owner is told "Group reservation was submitted."

    @api @negative
    Scenario Outline: Someone other than the owner cannot submit
      When <someone> submits the group pre-order
      Then <outcome>

      Examples:
        | someone                        | outcome                              |
        | another customer               | the reservation appears not to exist |
        | the waiter                     | the reservation appears not to exist |
        | a guest of the group           | they are asked to sign in            |
        | a visitor who is not signed in | they are asked to sign in            |
