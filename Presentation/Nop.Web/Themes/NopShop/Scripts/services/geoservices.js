var GeoLocator = function () {

    var handleGetAddressInfoByZip = function (zip,callback) {
        var addr = {};
        if (zip.length >= 5 && typeof google != 'undefined') {            
            var geocoder = new google.maps.Geocoder();
            geocoder.geocode({ 'address': zip }, function (results, status) {
                if (status == google.maps.GeocoderStatus.OK) {
                    if (results.length >= 1) {
                        for (var ii = 0; ii < results[0].address_components.length; ii++) {
                            var street_number = route = street = city = state = zipcode = country = formatted_address = '';
                            var types = results[0].address_components[ii].types.join(",");
                            if (types == "street_number") {
                                addr.street_number = results[0].address_components[ii].long_name;
                            }
                            if (types == "route" || types == "point_of_interest,establishment") {
                                addr.route = results[0].address_components[ii].long_name;
                            }
                            if (types == "sublocality,political" || types == "locality,political" || types == "neighborhood,political" || types == "administrative_area_level_3,political") {
                                addr.city = (city == '' || types == "locality,political") ? results[0].address_components[ii].long_name : city;
                            }
                            if (types == "administrative_area_level_1,political") {
                                addr.state = results[0].address_components[ii].short_name;
                            }
                            if (types == "postal_code" || types == "postal_code_prefix,postal_code") {
                                addr.zipcode = results[0].address_components[ii].long_name;
                            }
                            if (types == "country,political") {
                                addr.country = results[0].address_components[ii].long_name;
                            }
                        }
                        addr.success = true;
                        if (callback !== undefined) {
                            callback(addr);
                        }
                        for (name in addr) {
                            console.log('### google maps api ### ' + name + ': ' + addr[name]);
                        }
                        response(addr);
                    } else {
                        response({ success: false });
                    }
                } else {
                    response({ success: false });
                }
            });
        } else {
            response({ success: false });
        }
        return addr;
    };

    return {
        getAddressInfoByZip: function (zip,callback) {
            return handleGetAddressInfoByZip(zip,callback);
        }
    };
}();
 
function response(obj){
    console.log(obj);
}