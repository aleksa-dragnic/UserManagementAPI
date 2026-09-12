using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);

        // Ids are assigned by the domain (RefreshToken.Issue), never by EF Core or
        // the database. Same rule as every other entity: without it EF treats the
        // Guid key as store-generated and a token reached through a navigation,
        // key already set, would be tracked as Modified instead of Added.
        builder.Property(token => token.Id).ValueGeneratedNever();

        // SHA-256 as hex: always 64 characters.
        builder.Property(token => token.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        // Looked up by hash on every refresh; unique because two tokens hashing
        // alike would be indistinguishable on presentation.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Bulk revocation on reuse detection reads by user.
        builder.HasIndex(token => token.UserId);

        // PostgreSQL's xmin system column as a concurrency token: no column is
        // added, and any update whose row changed since it was read fails rather
        // than overwriting. Two requests that present the same refresh token at
        // the same moment would otherwise both rotate it and both succeed, which
        // hands out two live chains from one token.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(token => token.ExpiresAtUtc).IsRequired();
        builder.Property(token => token.CreatedAtUtc).IsRequired();

        // A foreign key to users, no navigation: RefreshToken is its own aggregate
        // and the reference stays an id. Cascade, because tokens without a user
        // mean nothing.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(token => token.IsExpired);
        builder.Ignore(token => token.IsRevoked);
        builder.Ignore(token => token.IsRotated);
        builder.Ignore(token => token.IsActive);
        builder.Ignore(token => token.DomainEvents);
    }
}