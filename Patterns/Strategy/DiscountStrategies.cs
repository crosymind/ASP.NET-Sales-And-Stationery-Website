using System;

namespace QuanLyKho.Patterns.Strategy
{
    public interface IDiscountStrategy
    {
        string Name { get; }
        decimal CalculateDiscount(decimal subtotal, decimal discountValue);
    }

    public class NoDiscountStrategy : IDiscountStrategy
    {
        public string Name => "None";

        public decimal CalculateDiscount(decimal subtotal, decimal discountValue)
        {
            return 0m;
        }
    }

    public class PercentageDiscountStrategy : IDiscountStrategy
    {
        public string Name => "Percentage";

        public decimal CalculateDiscount(decimal subtotal, decimal discountValue)
        {
            if (discountValue <= 0 || subtotal <= 0) return 0m;
            if (discountValue > 100) discountValue = 100;

            decimal discount = Math.Round(subtotal * (discountValue / 100m), 2);
            return Math.Min(discount, subtotal);
        }
    }

    public class FixedAmountDiscountStrategy : IDiscountStrategy
    {
        public string Name => "Fixed";

        public decimal CalculateDiscount(decimal subtotal, decimal discountValue)
        {
            if (discountValue <= 0 || subtotal <= 0) return 0m;
            return Math.Min(discountValue, subtotal);
        }
    }

    public static class DiscountStrategyFactory
    {
        public static IDiscountStrategy GetStrategy(string? discountType)
        {
            return discountType?.ToLower() switch
            {
                "percentage" => new PercentageDiscountStrategy(),
                "fixed" => new FixedAmountDiscountStrategy(),
                _ => new NoDiscountStrategy()
            };
        }
    }
}
