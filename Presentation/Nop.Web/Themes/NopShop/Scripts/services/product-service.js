var ProductService = {
    getProductReviews: function (productId) {
        var promise = $.getJSON("/getproductreviews/" + productId);
        return promise;
    },
    submitProductReview: function (url, data) {
        var promise = $.ajax({
            url: url,
            type: 'post',
            dataType: 'json',
            data: data,            
        });
        return promise;
    }
};