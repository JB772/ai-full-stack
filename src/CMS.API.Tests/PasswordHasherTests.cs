using CMS.API.Security;

namespace CMS.API.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesKnownSha256HexDigest()
    {
        // SHA-256("test") — the canonical vector, as an uppercase hex string.
        const string expected = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

        var hash = PasswordHasher.Hash("test");

        Assert.Equal(expected, hash, ignoreCase: true);
    }

    [Fact]
    public void Hash_Is64HexCharacters()
    {
        var hash = PasswordHasher.Hash("any-password-字元");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-Fa-f]{64}$", hash);
    }

    [Fact]
    public void Hash_IsDeterministic()
        => Assert.Equal(PasswordHasher.Hash("同一個密碼"), PasswordHasher.Hash("同一個密碼"));

    [Fact]
    public void Hash_DiffersByInput()
        => Assert.NotEqual(PasswordHasher.Hash("password-a"), PasswordHasher.Hash("password-b"));
}
