/*scroll to top*/

$(document).ready(function () {
    Layout.init();
    Layout.initTwitter();
    Layout.initImageZoom();
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
