using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.ToTable("user_achievements");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.AchievementCode).HasColumnName("achievement_code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.RewardCredits).HasColumnName("reward_credits").IsRequired();
        builder.Property(x => x.UnlockedAt).HasColumnName("unlocked_at").IsRequired();

        builder.HasIndex(x => new { x.UserId, x.AchievementCode })
            .IsUnique()
            .HasDatabaseName("uq_user_achievement_user_code");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
