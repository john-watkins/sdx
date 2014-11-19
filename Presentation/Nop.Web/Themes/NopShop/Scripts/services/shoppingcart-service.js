var ShoppingCartService = {
    removeItemFromCart: function (productId) {
        var data = {
            productId: productId
        }
        var promise = $.ajax({
            url: "/removeitemfromcart",
            type: 'post',
            dataType: 'json',
            data: data,
        });
        return promise;
    }    
};