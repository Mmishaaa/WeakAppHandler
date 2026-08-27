using WeakAppHandler.Auth.Security;

namespace WeakAppHandler.Auth.Tests;

public class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = Pbkdf2PasswordHasher.Hash("correct-horse-battery-staple");

        Assert.True(Pbkdf2PasswordHasher.Verify("correct-horse-battery-staple", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = Pbkdf2PasswordHasher.Hash("correct-horse-battery-staple");

        Assert.False(Pbkdf2PasswordHasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFalseInsteadOfThrowing()
    {
        Assert.False(Pbkdf2PasswordHasher.Verify("anything", "not-a-valid-hash"));
    }

    [Fact]
    public void Hash_SamePasswordDifferentCalls_ProducesDifferentSaltedOutput()
    {
        var first = Pbkdf2PasswordHasher.Hash("same-password");
        var second = Pbkdf2PasswordHasher.Hash("same-password");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_ExplicitSaltAndIterations_IsDeterministic()
    {
        var salt = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

        var first = Pbkdf2PasswordHasher.Hash("seed-password", salt, Pbkdf2PasswordHasher.DefaultIterations);
        var second = Pbkdf2PasswordHasher.Hash("seed-password", salt, Pbkdf2PasswordHasher.DefaultIterations);

        Assert.Equal(first, second);
    }
}
