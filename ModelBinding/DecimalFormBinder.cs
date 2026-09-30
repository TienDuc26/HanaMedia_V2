using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HanaMedia.ModelBinding;

// HTML number fields submit a decimal dot even on a Vietnamese Windows host.
// Accept a single decimal comma for legacy forms, but never treat it as a grouping separator.
public sealed class DecimalFormBinder : IModelBinder, IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?) ? this : null;

    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        var text = value.FirstValue?.Trim();
        if (string.IsNullOrEmpty(text) && context.ModelType == typeof(decimal?))
            context.Result = ModelBindingResult.Success(null);
        else if (decimal.TryParse(text?.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var amount)) context.Result = ModelBindingResult.Success(amount);
        else context.ModelState.TryAddModelError(context.ModelName, "Số tiền không hợp lệ. Không nhập dấu phân cách hàng nghìn.");
        return Task.CompletedTask;
    }
}
