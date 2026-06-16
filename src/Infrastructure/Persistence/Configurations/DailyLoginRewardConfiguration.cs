using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;
using OsuStocks.Infrastructure.Persistence.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class DailyLoginRewardConfiguration : IEntityTypeConfiguration<DailyLoginReward>
{
    public void Configure(EntityTypeBuilder<DailyLoginReward> builder)
    {
        builder.ToTable("daily_login_rewards");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.RewardDate).HasColumnName("reward_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.StreakDay).HasColumnName("streak_day").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // Authoritative idempotency guard: at most one reward per user per UTC day. The repository keys its
        // duplicate-claim detection on this exact constraint name.
        builder.HasIndex(x => new { x.UserId, x.RewardDate })
            .IsUnique()
            .HasDatabaseName(DailyLoginRewardRepository.UniqueConstraintName);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
