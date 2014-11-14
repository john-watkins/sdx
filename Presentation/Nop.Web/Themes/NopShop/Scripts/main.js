/*scroll to top*/

$(document).ready(function () {
    Layout.init();
    Layout.initTwitter();
    Layout.initImageZoom();
    Layout.initTouchspin();
    Layout.initUniform();
    $('#sl2').slider();

    var RGBChange = function () {
        $('#RGB').css('background', 'rgb(' + r.getValue() + ',' + g.getValue() + ',' + b.getValue() + ')')
    };	
});
