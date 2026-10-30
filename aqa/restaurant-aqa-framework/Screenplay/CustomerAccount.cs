namespace Restaurant_Aqa_Framework.Screenplay;

/// <summary>A customer the scenario registered and signed in — the owner of a group booking, or another customer
/// who has nothing to do with it. Remembered under <see cref="Role"/>.</summary>
public sealed record CustomerAccount(string Role, string FirstName, string Email, string Password, string IdToken)
{
    public const string Owner = "owner";
    public const string AnotherCustomer = "another customer";
}
