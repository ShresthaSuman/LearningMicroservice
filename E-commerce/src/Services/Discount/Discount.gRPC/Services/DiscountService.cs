using Discount.gRPC.Data;
using Discount.gRPC.Model;
using Grpc.Core;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Discount.gRPC.Services;

public class DiscountService(DiscountContext dbContext , ILogger<DiscountService> logger) : DiscountProtoService.DiscountProtoServiceBase
{
    public  override async Task<CouponModel> GetDiscount(GetDiscountRequest request, ServerCallContext context)
    {
        var coupon = await dbContext.Coupons.FirstOrDefaultAsync(x => x.ProductName == request.ProductName);
        if (coupon is null)
        {
            coupon = new Coupon{ ProductName = "No discount", Amount = 0,Description = "No discount in this product" };
        }
        logger.LogInformation("Product name: {productName} ,amount: {amount}, description:{description}", coupon.ProductName, coupon.Amount,coupon.Description);
        var couponModel = coupon.Adapt<CouponModel>();
        return couponModel;
    }

    public override async Task<CouponModel> CreateDiscount(CreateDiscountRequest request, ServerCallContext context)
    {
        var coupon = request.Coupon.Adapt<Coupon>();
        if (coupon is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,"Failed to create coupon"));
        }
        dbContext.Coupons.Add(coupon);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Created discount coupon: {coupon}", coupon);
        return coupon.Adapt<CouponModel>();
    }

    public override async Task<CouponModel> UpdateDiscount(UpdateDiscountRequest request, ServerCallContext context)
    {
        var coupon = request.Coupon.Adapt<Coupon>();
        if (coupon is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,"Failed to create coupon"));
        }
        dbContext.Coupons.Update(coupon);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Created discount coupon: {coupon}", coupon);
        return coupon.Adapt<CouponModel>();
    }

    public override async Task<DeleteDiscountRespose> DeleteDiscount(DeleteDiscountRequest request, ServerCallContext context)
    {
        
        var coupon = await dbContext.Coupons.FirstOrDefaultAsync(x=> x.ProductName==request.ProductName);
        if (coupon is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,"Coupon not found"));
        }
        dbContext.Coupons.Remove(coupon);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Deleted discount of product: {ProductName}", request.ProductName);
        return new DeleteDiscountRespose{Success = true};
    }
}   