/*scroll to top*/

$(document).ready(function () {                
    NopTronic.init();
});

var NopTronic = function () {

    var handleInit = function () {
        Layout.init();
        Layout.initTwitter();
        Layout.initImageZoom();
        Layout.initOWL();
        Layout.initTouchspin();
        Layout.initUniform();
        $(".search-btn").click(function () {
            $("#small-searchterms").focus();
        });
        $('#sl2').slider();
    };
    var handleFormValidationInit = function () {
        $('form.run-form-validation').each(function () {
            var t = $(this);
            var id = t.attr("id");
            $("#" + id).validate();
        });
    };
    var initCheckoutWizard = function () {
        if ($('#form_wizard_checkout').length > 0) {
            FormWizard.init();
        }
    };

    return {
        scrollTo: function (el, offset) {
            setTimeout(function () {
                $('html, body').animate({
                    scrollTop: el.offset().top - offset
                }, 200);
            },300);            
        },
        init: function () {
            handleInit();
            handleFormValidationInit();
            initCheckoutWizard();
        }
    };
}();
