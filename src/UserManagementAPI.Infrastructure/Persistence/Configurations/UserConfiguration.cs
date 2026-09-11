using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        // Ids are assigned by the domain (Entity constructor), never by EF Core or
        // the database. Without this EF treats the Guid key as store-generated and
        // tracks a child discovered through a navigation, with its key already
        // set, as Modified instead of Added - the UPDATE then affects zero rows.
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.OwnsOne(user => user.Email, email =>
        {
            email.Property(value => value.Value)
                .HasColumnName("email")
                .HasColumnType("citext")
                .IsRequired();

            email.HasIndex(value => value.Value).IsUnique();
        });
        builder.Navigation(user => user.Email).IsRequired();

        builder.OwnsOne(user => user.Name, name =>
        {
            name.Property(value => value.First)
                .HasColumnName("name_first")
                .HasMaxLength(PersonName.MaxPartLength)
                .IsRequired();

            name.Property(value => value.Last)
                .HasColumnName("name_last")
                .HasMaxLength(PersonName.MaxPartLength)
                .IsRequired();
        });
        builder.Navigation(user => user.Name).IsRequired();

        builder.OwnsOne(user => user.PasswordHash, hash =>
        {
            hash.Property(value => value.Value)
                .HasColumnName("password_hash")
                .IsRequired();
        });
        builder.Navigation(user => user.PasswordHash).IsRequired();

        // UserStatus is an enumeration class, so the column stores its stable int Id.
        builder.Property(user => user.Status)
            .HasColumnName("status_id")
            .HasConversion(
                status => status.Id,
                id => Enumeration.FromId<UserStatus>(id))
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc).IsRequired();
        builder.Property(user => user.UpdatedAtUtc).IsRequired();

        builder.HasMany(user => user.Roles)
            .WithOne()
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The collection has no public setter; EF writes through the backing field.
        builder.Metadata
            .FindNavigation(nameof(User.Roles))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(user => user.DomainEvents);
    }
}