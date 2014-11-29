var _reviewsTemplate = null;
$(document).ready(function () {
    var source = $("#reviews-list-template").html();
    _reviewsTemplate = Handlebars.compile(source);
    ProductTemplateController.init();
});

var ProductTemplateController = function() {
    var _this = this;
    var handleInit = function () {
        $('a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
            var ele = $(e.target);
            var target = ele.attr("href") // activated tab            
            if (target == "#Reviews") {
                var isLoaded = $("#reviews_placeholder").data("loaded") == "true";
                if (!isLoaded) {
                    var productId = $(this).data("productid");
                    handleLoadReviews(productId);
                }                
            }
            NopTronic.scrollTo(ele, 60);
        });
        handleSubmitReview();

    }
    var handleLoadReviews = function (productId) {
        ProductService.getProductReviews(productId)
               .done(function (data) {
                   var html = _reviewsTemplate(data);
                   var ele = $("#reviews_placeholder");
                   ele.html(html).data("loaded", "true");
                   $('.rateit',ele).rateit();
               })
               .fail(function (data) {

               });
    }

    var handleSubmitReview = function(){

        $("#submit_review_form").submit(function (e) {
            e.preventDefault();
            var actionurl = e.currentTarget.action;
            var data = $(this).serialize();                        
            data += "&add-review=1&captchaValid=true";
            ProductService.submitProductReview(actionurl, data)
               .done(function (data) {
                   window.location.reload();
               })
               .fail(function (data) {
                   var s = "";
               })
               .always(function(data){
                   window.location.reload();
               });
        });
    }

    var handleAddReviewClick = function () {
        var ele = $('#myTab a[href="#AddReview"]');
        ele.tab('show');
        var ele2 = $("#AddReview");
        NopTronic.scrollTo(ele2, 60);
    }

    var hadleVariantImgClick = function (ele) {        
        var productId = ele.data("productid");
        var fullSizeImgUrl = ele.data("bigimgsrc");
        var imgUrl = ele.attr("src");
        $("#main-product-img-" + productId).attr("src", imgUrl).attr("data-bigimgsrc", fullSizeImgUrl)        
        $('.product-main-image').trigger('zoom.destroy');
        Layout.initImageZoom();
    }

    return {
        init: function () {
            handleInit(); 
        },
        loadReviews: function (productId) {
           handleLoadReviews(productId)
        },
        beforeSubmitReview: function () {
            handleBeforeSubmitReview();
        },
        addReviewClick: function () {
            handleAddReviewClick();
        },
        variantImgClick: function (ele) {
            hadleVariantImgClick($(ele));
        }
    };
}();
