namespace UserManagementAPI.IntegrationTests;

/// <summary>
/// One container for every integration test. Starting a container per class
/// would multiply a slow setup by the number of test classes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}