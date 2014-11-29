var ShippingService = {
    getShippingRates: function (shippingInfo) {
        var promise = $.ajax({
            url: "/GetShippingOptionRatesJson",
            type: 'post',
            dataType: 'json',
            data: shippingInfo,
        });
        return promise;        
    }    
};