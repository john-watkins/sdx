/*scroll to top*/

$(document).ready(function () {
    Layout.init();
    Layout.initTwitter();
    Layout.initImageZoom();
    Layout.initOWL();
    Layout.initTouchspin();
    Layout.initUniform();
    $('#sl2').slider();
    $('form.run-form-validation').each(function(){
        var t = $(this);
        var id = t.attr("id");
        $("#"+id).validate();
    });
    var RGBChange = function () {
        $('#RGB').css('background', 'rgb(' + r.getValue() + ',' + g.getValue() + ',' + b.getValue() + ')')
    };
    $(".search-btn").click(function () {
        $("#small-searchterms").focus();
    });
    
});

var NopTronic = function () {

    return {
        scrollTo: function (el, offset) {
            setTimeout(function () {
                $('html, body').animate({
                    scrollTop: el.offset().top - offset
                }, 200);
            },300);            
        }
    };
}();
