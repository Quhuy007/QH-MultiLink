using QH.MultiLinkShortener.Core;
using QH.MultiLinkShortener.Infrastructure;
using Xunit;

namespace QH.MultiLinkShortener.Tests;

public class ConnectionDraftTests
{
    private static readonly ProviderInfo[] Connections =
    [
        new("spoo.me", "spoo.me", "", true, new("https://spoo.me/"), "spoo"),
        new("tinyurl", "TinyURL", "", false, new("https://tinyurl.com/"), "tinyurl", true),
        new("shortio", "Short.io", "", true, new("https://short.io/"), "shortio", true, true)
    ];

    [Fact]
    public void SwitchingServicesKeepsIndependentDraftsAndSaveAllPersistsBoth()
    {
        var secrets = new MemorySecrets();
        var editor = new ConnectionDrafts(secrets, Connections);
        editor.Set("spoo", new("spoo_draft"));
        Assert.Equal("", editor.Get("tinyurl").Key);
        editor.Set("tinyurl", new("tiny_draft"));
        Assert.Equal("spoo_draft", editor.Get("spoo").Key);
        Assert.Equal(2, editor.SaveAll());
        Assert.Equal("spoo_draft", secrets.Read("spoo"));
        Assert.Equal("tiny_draft", secrets.Read("tinyurl"));
        Assert.False(editor.HasChanges("spoo"));
        Assert.True(editor.HasSavedKey("spoo"));
        Assert.Equal("", editor.Get("spoo").Key);
    }

    [Fact]
    public void SavingOneServiceKeepsOtherDraftAndNeverRevealsSavedKeys()
    {
        var secrets = new MemorySecrets();
        secrets.Write("spoo", "spoo_old");
        var editor = new ConnectionDrafts(secrets, Connections);
        Assert.Equal("", editor.Get("spoo").Key);
        editor.Set("tinyurl", new("tiny_pending"));
        editor.Set("spoo", new("spoo_new"));
        Assert.True(editor.Save("spoo"));
        Assert.Equal("tiny_pending", editor.Get("tinyurl").Key);
        Assert.True(editor.HasChanges("tinyurl"));
        Assert.Null(secrets.Read("tinyurl"));
        Assert.False(editor.Save("spoo"));
        Assert.Equal("spoo_new", secrets.Read("spoo"));
    }

    [Fact]
    public void InvalidDraftAndWriteFailureRetainInputForCorrection()
    {
        var secrets = new MemorySecrets();
        var editor = new ConnectionDrafts(secrets, Connections);
        editor.Set("spoo", new("spoo_valid"));
        editor.Set("shortio", new("public_test", "https://wrong.example/path"));
        Assert.Throws<ArgumentException>(() => editor.SaveAll());
        Assert.Null(secrets.Read("spoo"));
        Assert.Equal("public_test", editor.Get("shortio").Key);
        secrets.FailWrite = true;
        Assert.Throws<IOException>(() => editor.Save("spoo"));
        Assert.Equal("spoo_valid", editor.Get("spoo").Key);
    }

    [Fact]
    public void DomainCanChangeWithoutRetypingAnExistingKey()
    {
        var secrets = new MemorySecrets();
        secrets.Write("shortio", new ServiceCredential("public_test", "old.example").Encode());
        var editor = new ConnectionDrafts(secrets, Connections);
        Assert.Equal(new ConnectionDraft("", "old.example"), editor.Get("shortio"));
        editor.Set("shortio", new("", "new.example"));
        editor.Save("shortio");
        Assert.Equal(new ServiceCredential("public_test", "new.example"), ServiceCredential.Decode(secrets.Read("shortio")));
    }

    [Fact]
    public void RemovingAConnectionDoesNotTouchOtherSavedOrPendingKeys()
    {
        var secrets = new MemorySecrets();
        secrets.Write("spoo", "spoo_saved"); secrets.Write("tinyurl", "tiny_saved");
        var editor = new ConnectionDrafts(secrets, Connections);
        editor.Set("tinyurl", new("tiny_pending"));
        editor.Remove("spoo");
        Assert.False(editor.HasSavedKey("spoo"));
        Assert.Equal("tiny_saved", secrets.Read("tinyurl"));
        Assert.Equal("tiny_pending", editor.Get("tinyurl").Key);
    }

    [Fact]
    public void MultipleEncryptedConnectionsSurviveRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QH-connections-" + Guid.NewGuid());
        try
        {
            var editor = new ConnectionDrafts(new DpapiSecretStore(directory), Connections);
            editor.Set("spoo", new("spoo_test_only")); editor.Set("tinyurl", new("tiny_test_only"));
            editor.SaveAll();
            var restored = new ConnectionDrafts(new DpapiSecretStore(directory), Connections);
            Assert.True(restored.HasSavedKey("spoo")); Assert.True(restored.HasSavedKey("tinyurl"));
            Assert.Equal("", restored.Get("spoo").Key); Assert.Equal("", restored.Get("tinyurl").Key);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class MemorySecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = [];
        public bool FailWrite { get; set; }
        public string? Read(string name) => _values.GetValueOrDefault(name);
        public void Write(string name, string? value)
        {
            if (FailWrite) throw new IOException("Test storage failure");
            if (value is null) _values.Remove(name); else _values[name] = value;
        }
    }
}
