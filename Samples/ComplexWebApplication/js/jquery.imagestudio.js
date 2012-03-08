﻿(function ($) {

    //Polling methods
    //$('obj').ImageStudio('api').getStatus({'restoreSuspendedCommands':true, 'removeEditingConstraints':true, 'useEditingServer':false} ) returns { url, path, query };
    //$('obj').ImageStudio('api').setOptions({height:600});
    //$('obj').ImageStudio('api').setOptions({url:'newimageurl.jpg'}); //Yes, you can switch images like this.. as long as you're not in the middle of cropping. That's not supported yet.
    //$('obj').ImageStudio('api').getOptions();
    //$('obj').ImageStudio('api').destroy();
    var defaults = {
        url: null, //The image URL to load for editing. 
        width: null, //To set the width of the area
        height: 530, //To constrain the height of the area.
        panes: ['rotateflip', 'crop', 'adjust', 'redeye', 'carve', 'effects'], //A list of panes to display, in order. 
        editingServer: null, //If set, an alternate server will be used during editing. For example, using a cloud front distribution during editing is counter productive
        editWithSemicoloms: false, //If true, semicolon notation will be used with the editing server. 
        finalWithSemicolons: false, //If true, semicolons will be used in the final URLs. Defaults to true if the input URL uses semicolons.
        //A list of commands to temporarily remove from the URL during editing so that position-dependent operations aren't affected.
        suspendKeys: ['width','height','maxwidth','maxheight',
                       'scale', 'rotate', 'flip', 'anchor', 
                       'paddingwidth', 'paddingcolor','borderwidth','bordercolor','margin', 
                       'cache','process', 'shadowwidth', 'shadowcolor', 'shadowoffset'], 
        onchange: null, //The callback to fire whenever an edit occurs.
        cropratios: [[0, "Custom"], ["current", "Current"], [4 / 3, "4:3"], [16 / 9, "16:9 (Widescreen)"], [3 / 2, "3:2"]],
        icons: { 
            rotateleft: 'arrowreturnthick-1-w',
            rotateright: 'arrowreturnthick-1-e',
            flipvertical: 'arrowthick-2-n-s',
            fliphorizontal: 'arrowthick-2-e-w',
            reset: 'cancel',
            autofix:'image',
            blackwhite:'image',
            sepia:'image',
            negative:'image',

        },
        labels: {
            pane_rotateflip: 'Rotate &amp; Flip',
            rotateleft: 'Rotate Left',
            rotateright: 'Rotate Right',
            flipvertical: 'Flip Vertical',
            fliphorizontal: 'Flip Horizontal',
            reset: 'Reset',
            pane_crop: 'Crop',
            aspectratio: 'Aspect Ratio',
            crop_crop: 'Crop',
            crop_modify: 'Modify Crop',
            crop_cancel: 'Cancel',
            crop_done: 'Done',
            pane_adjust: 'Adjust Image',
            autofix:'Auto-Fix',
            contrast: 'Contrast',
            saturation: 'Saturation',
            brightness: 'Brightness',
            pane_effects: 'Effects &amp; Filters',
            blackwhite: 'Black & White',
            sepia: 'Sepia',
            negative: 'Negative',
            sharpen:'Smart Sharpen',
            noiseremoval: 'Noise Removal',
            oilpainting: 'Oil Painting',
            posterize: 'Posterize',
            blur: 'Gaussian Blur',
            pane_redeye: 'Red-Eye Removal',
            redeyeauto: 'Automatic',
            cancel: 'Cancel',
            done: 'Done',
            pane_carve: 'Object Removal',

       }
    };

    $.fn.ImageStudio = function (options) {

        function processOptions(options){
            var defs = $.extend({},defaults);
            if (options.url.indexOf('?') < 0 && options.url.indexOf(';') > -1) defs.finalWithSemicolons = true;
            defs.editWithSemicolons = defs.finalWithSemicolons;
           return $.extend(true,{},defaults, options);
        }

        return this.each(function () {
            div = $(this);
            
            if (div.data('ImageStudio')) {
                // The API can be requested this way (undocumented)
                if (options === 'api') return $(this).data('ImageStudio');
                // Otherwise, we just reset the options...
                else $(this).data('ImageStudio').setOptions(processOptions(options));
            }else {
                $(this).data('ImageStudio',init($(this),processOptions(options)));
            }
        });
    };

    function init(div, opts){
        div = $(div); 
        div.empty();
        div.removeClass("imagestudio"); div.addClass("imagestudio");

        //UGH! a table. But the alternative is nasty cross-browser.
        var tr = $('<tr></tr>').appendTo($('<table></table').appendTo(div));

        //Add accordion
        var a = $("<div></div>").addClass("controls").width(230).appendTo($('<td></td>').appendTo(tr));
        //Add image
        var img = $('<img />').addClass("studioimage").appendTo($('<td></td>').css('vertical-align','middle').css('text-align','center').css('padding-left','20px').appendTo(tr));
        opts.img = img; //Save a reference to the image object in options
        opts.accordion = a;
        var updateOptions = function(){
            if (opts.width) div.width(opts.width);
            if (opts.height) div.height(opts.height);
            if (opts.height) a.height(opts.height);
            opts.original = ImageResizing.Utils.parseUrl(opts.url);
            opts.editPath = opts.original.path; 
            if (opts.editingServer) opts.editPath = ImageResizing.Utils.changeServer(opts.editPath,opts.editingServer);

            opts.originalQuery = opts.original.obj;
            opts.filteredQuery = new ImageResizing.ResizeSettings(opts.originalQuery);
            opts.suspendedItems = opts.filteredQuery.remove(opts.suspendKeys);
            var withConstraints = new ImageResizing.ResizeSettings(opts.filteredQuery); 
            withConstraints.maxwidth = div.width() - 250;
            withConstraints.maxheight = opts.height;
            
            opts.editQuery = withConstraints;
            opts.editUrl = opts.editPath + withConstraints.toQueryString(opts.editWithSemicolons);
            
            img.attr('src', opts.editUrl);
            img.triggerHandler('query', [new ImageResizing.ResizeSettings(opts.editUrl)]);

        }; updateOptions();

        //Add requested panes
        var panes = {'rotateflip':addRotateFlipPane, 'crop':addCropPane,'adjust':addAdjustPane,'redeye':addRedEyePane,'carve':addCarvePane,'effects':addEffectsPane};
        for (var i = 0; i < opts.panes.length; i++){
            a.append('<h3><a href="#">' + opts.labels['pane_' + opts.panes[i]] + '</a></h3>');
            a.append(panes[opts.panes[i]](opts));

        }
        //Activate accordion
        a.accordion({  fillSpace:true});

        var api = {
            getOptions: function(){ return opts;},
            setOptions: function(newOpts){
                $.extend(opts,newOpts);
                updateOptions();
            },
            getStatus: function(params){
                $.extend(params, {'restoreSuspendedCommands':true, 'removeEditingConstraints':true, 'useEditingServer':false});
                var path = params.useEditingServer ? opts.editPath : opts.original.path;

                var q = new ImageResizing.ResizeSettings(opts.editQuery); 
                if (params.removeEditingConstraints) q.remove(opts.suspendKeys);
                if (params.restoreSuspendedCommands) q.mergeWith(opts.suspendedItems);

                var url = params.useEditingServer ? q.toQueryString(opts.editWithSemicolons) : q.toQueryString(opts.finalWithSemicolons);
                return {url:url, query:q,path:path};
            },
            destroy: function(){
                div.data('ImageStudio',null);
                div.empty();
            }};
        opts.api = api;
        return api;
    }
    

//Provides a callback to edit the querystring inside
var edit = function (opts, callback) {
    opts.editQuery = new ImageResizing.ResizeSettings(opts.editQuery);
    callback(opts.editQuery);
    opts.editUrl = opts.editPath + opts.editQuery.toQueryString(opts.editWithSemicolons);
    opts.img.attr('src', opts.editUrl);
    opts.img.triggerHandler('query', [opts.editQuery]);
    if (opts.onchange != null) opts.onchange(opts.api);
};
var setUrl = function(opts, url, silent){
    opts.editQuery = new ImageResizing.ResizeSettings(url) ;
    opts.editUrl = url;
    opts.img.attr('src', url);
    if (!silent) {
        opts.img.triggerHandler('query', [new ImageResizing.ResizeSettings(opts.editQuery)]);
        if (opts.onchange != null) opts.onchange(opts.api);
    }
}
//Makes a button that edits the image's querystring.
var button = function (opts, id, editCallback, clickCallback) {
    var icon = opts.icons[id];
    var b = $('<button type="button"></button>').addClass('button_' + id).button({ label: opts.labels[id], icons: icon != null ? { primary: "ui-icon-" + icon} : {}});
    if (editCallback) b.click(function () {
        edit(opts, function (obj) {
            editCallback(obj);
        });
    });

    if (clickCallback) b.click(clickCallback);
    return b;
};
var toggle = function (container, id, querystringKey, opts) {
    if (!window.uniqueId) window.uniqueId = (new Date()).getTime();
    window.uniqueId++;
    var chk = $('<input type="checkbox" id="' + window.uniqueId + '" />');
    chk.prop("checked", opts.editQuery.getBool(querystringKey));
    chk.appendTo(container);
    $('<label for="' + window.uniqueId + '">' + opts.labels[id] + '</label>').appendTo(container);
    chk.button({ icons: { primary: "ui-icon-" + opts.icons[id]} }).click(function () {
        edit(opts, function (obj) {
            obj.toggle(querystringKey);
        });
    });
    opts.img.bind('query', function (e, obj) {
        var b = obj.getBool(querystringKey);
        if (chk.prop("checked") != b) chk.prop("checked", b);
        chk.button('refresh');
    });
    return chk;
};
var slider = function (opts, min, max, step, key) {
    var supress = {};
    var startingValue = opts.editQuery[key]; if (startingValue == null) startingValue = 0;
    var s = $("<div></div>").slider({ min: min, max: max, step: step, value: startingValue,
        change: function (event, ui) {
            supress[key] = true;
            edit(opts, function (obj) {
                obj[key] = ui.value;
                if (key.charAt(0) == 'a') obj['a.radiusunits'] = 1000;
                if (obj[key] === 0) delete obj[key];
            });
            supress[key] = false;
        }
    });
    opts.img.bind('query', function (e, obj) {
        if (supress[key]) return;
        var v = obj[key]; if (v == null) v = 0;
        if (v != s.slider('value')) {
            s.slider('value', v);
        }
    });
    return s;
};
var h3 = function(opts,id,container){
    return $("<h3 />").text(opts.labels[id] ? opts.labels[id] : "text-not-set").addClass(id).appendTo(container);
};
//Adds a pane for rotating and flipping the source image
var addRotateFlipPane = function (opts) {
    var c = $('<div></div>');
    button(opts, 'rotateleft', function (obj) { obj.increment("srotate", -90, 360);}).appendTo(c);
    button(opts, 'rotateright',  function (obj) { obj.increment("srotate", 90, 360);}).appendTo(c);
    button(opts, 'flipvertical', function (obj) {obj.toggle("sflip.y");}).appendTo(c);
    button(opts, 'fliphorizontal', function (obj) { obj.toggle("sflip.x");}).appendTo(c);
    button(opts, 'reset', function (obj) { obj.remove("srotate","sflip"); }).appendTo(c);


//    var lRot = $("<h3></h3>").appendTo(c).hide();
//    var updateLabels = function (e, obj) {
//        var f = ImageResizing.Utils.parseFlip(obj["sflip"]);
//        lRot.text("Image rotated " + (!obj["srotate"] ? 0 : (obj["srotate"] % 360)) + " degrees and " +
//         (f.x ? ("flipped horizontally " + (f.y ? " and vertically" : "")) : (f.y ? "flipped vertically" : "not flipped")));
//    }
//    updateLabels(null, getObj(opts));
//    opts.img.bind('query', updateLabels);
    return c;
};

var addAdjustPane = function (opts) {
    var c = $('<div></div>');
    toggle( c, 'autofix', "a.equalize", opts);
    h3(opts,'contrast',c);
    c.append(slider(opts,-1,1,0.001,"s.contrast"));
    h3(opts,'saturation',c);
    c.append(slider(opts, -1, 1, 0.001, "s.saturation"));
    h3(opts,'brightness',c);
    c.append(slider(opts, -1, 1,  0.001, "s.brightness"));
    button(opts, 'reset', function (obj) {
        obj.remove("s.contrast","s.saturation","s.brightness","a.equalize");
    }).appendTo(c);
    return c;
};

var addEffectsPane = function (opts) {
    var c = $('<div></div>');
    toggle(c, 'blackwhite', "s.grayscale", opts);
    toggle(c, "sepia",  "s.sepia", opts);
    toggle(c, "negative", "s.invert", opts);
    h3(opts,'sharpen',c);
    c.append(slider(opts, 0, 15, 1, "a.sharpen"));
    h3(opts,'noiseremoval',c);
    c.append(slider(opts, 0, 100, 1, "a.removenoise"));
    h3(opts,'oilpainting',c);
    c.append(slider(opts, 0, 25, 1, "a.oilpainting"));
    h3(opts,'posterize',c);
    c.append(slider(opts, 0, 255, 1, "a.posterize"));
    h3(opts,'blur',c);
    c.append(slider(opts, 0, 40, 1, "a.blur"));
    button(opts,'reset', function (obj) {
        obj.remove("a.sharpen","a.removenoise","a.oilpainting","a.posterize","s.grayscale","s.sepia","s.invert","a.blur","a.radiusunits");
    }).appendTo(c);

    return c;
};

var addRedEyePane = function (opts) {
    var c = $('<div></div>');
    toggle(c, 'redeyeauto', "r.auto", opts);

//    button(img, "Select eyes", "pencil", function (obj) {

//    }).appendTo(c);

//    button(img, "Done", null, function (obj) {

//    }).appendTo(c);

//    button(img, "Cancel", null, function (obj) {

//    }).appendTo(c);

//    button(opts, 'reset', function (obj) {

//    }).appendTo(c);
    return c;
};

var addCarvePane = function (opts) {
    var c = $('<div></div>');

//    button(img, "Remove objects", null, function (obj) {

//    }).appendTo(c);
//    
//    button(img, "Mark areas to remove", "pencil", function (obj) {

//    }).appendTo(c);

//    button(img, "Mark areas to preserve", null, function (obj) {

//    }).appendTo(c);
//    c.append("<h3>Brush size</h3>");
//    $("<div></div>").slider({ min: 0, max: 15, value: 0,
//        change: function (event, ui) {
//            edit(opts, function (obj) {
//                obj["a.sharpen"] = ui.value;
//            });
//        }
//    }).appendTo(c);

//    button(img, "Clear", null, function (obj) {

//    }).appendTo(c);

//    button(img, "Done", null, function (obj) {

//    }).appendTo(c);

//    button(img, "Reset", null, function (obj) {

//    }).appendTo(c);

    return c;
};
//Adds a pane for cropping
var addCropPane = function (opts) {
     var c = $('<div></div>');
     var img = opts.img;

    var cropping = false;
    var jcrop_reference
    var previousUrl = null;

    var startCrop = function (uncroppedWidth, uncroppedHeight, uncroppedUrl) {
        cropping = true;

        btnCrop.hide();
        //Prevent the accordion from changing, but don't gray out this panel
        opts.accordion.accordion("disable");
        c.removeClass("ui-state-disabled");
        c.removeClass("ui-accordion-disabled");
        opts.accordion.removeClass("ui-state-disabled");


        //Get the original crop values and URL
        var obj = opts.editQuery;
        previousUrl = opts.editUrl;

        //Switched to uncropped image
        img.attr('src', uncroppedUrl);
        img.data('obj', null);

        //Start jcrop


        //Use existing coords if present
        var coords = null;
        if (obj["crop"] && obj["cropxunits"] && obj["cropyunits"]) {
            coords = obj["crop"].split(',');
            for (var i = 0; i < coords.length; i++) coords[i] = parseInt(coords[i]);
            var xfactor = uncroppedWidth / obj["cropxunits"];
            var yfactor = uncroppedHeight / obj["cropyunits"];
            coords[0] *= xfactor;
            coords[2] *= xfactor;
            coords[1] *= yfactor;
            coords[3] *= yfactor;
        }

        preview.JcropPreview({ jcropImg: img });
        preview.hide();

        var update = function (coords) {
            preview.JcropPreviewUpdate(coords);
            preview.show();
        };

        //Start up jCrop
        img.Jcrop({
            onChange: update,
            onSelect: update,
            aspectRatio: getRatio(),
            bgColor: 'black',
            bgOpacity: 0.6
        }, function () {
            jcrop_reference = this;

            preview.JcropPreviewUpdate({ x: 0, y: 0, x2: uncroppedWidth, y2: uncroppedHeight, width: uncroppedWidth, height: uncroppedHeight });
            if (coords != null) this.setSelect(coords);

            btnReset.hide();

            //Show buttons
            btnCancel.show();
            btnDone.show();
            label.show();
            ratio.show();
            

        });

    }


    var stopCrop = function (save) {
        cropping = false;
        if (save) {
            setUrl(opts, previousUrl, true);
            var coords = jcrop_reference.tellSelect();
            edit(opts, function (obj) {
                obj['crop'] = coords.x + ',' + coords.y + ',' + coords.x2 + ',' + coords.y2;
                obj['cropxunits'] = img.width();
                obj['cropyunits'] = img.height();
            });
        } else {
            setUrl(opts, previousUrl);
        }
        jcrop_reference.destroy();
        img.attr('style', ''); //Needed to fix all the junk JCrop added.
        btnCancel.hide();
        btnDone.hide();
        label.hide();
        ratio.hide();
        preview.hide();

        btnCrop.show();
        btnReset.show();
        
        opts.accordion.accordion("enable");
    }

    var btnCrop = button(opts,'crop_crop',null, function () {
        var q = new ImageResizing.ResizeSettings(opts.dataQuery);
        q.remove("crop","cropxunits","cropyunits");
        var uncroppedUrl = opts.editPath + q.toQueryString(opts.editWithSemicolons);
        var image = new Image();
        image.onload = function () { startCrop(image.width, image.height, uncroppedUrl); };
        image.src = uncroppedUrl;
    }).appendTo(c);

    var label = h3(opts,'aspectratio',c).hide();
    var ratio = $("<select></select>");
    var getRatio = function () {
        return ratio.val() == "current" ? img.width() / img.height() : (ratio.val() == 0 ? null : ratio.val())
    }
    var ratios = opts.cropratios;
    for (var i = 0; i < ratios.length; i++)
        $('<option value="' + ratios[i][0].toString() + '">' + ratios[i][1] + '</option>').appendTo(ratio);
    ratio.appendTo(c).val(0).hide();
    ratio.change(function () {
        jcrop_reference.setOptions({ aspectRatio: getRatio() });
        jcrop_reference.focus();
    });

    var btnCancel = button(opts,'crop_cancel', null, function (obj) {
        stopCrop(false);
    }).appendTo(c).hide();
    var btnDone = button(opts, 'crop_done', null, function (obj) {
        stopCrop(true);
    }).appendTo(c).hide();
    var preview = $("<div style='width:200px;height:200px;margin-left:-15px'></div>").appendTo(c).hide();
    var btnReset = button(opts,'reset', function (obj) {
        stopCrop(false);
        obj.remove("crop","cropxunits","cropyunits");
    }).appendTo(c);



    //Update button label and 'undo' visib
    btnCrop.button("option", "label", opts.editQuery.crop ? opts.labels.crop_modify : opts.labels.crop_crop);
    btnReset.button({ disabled: !opts.editQuery.crop });
    img.bind('query', function (e, obj) {
        btnCrop.button("option", "label", obj["crop"] ? opts.labels.crop_modify : opts.labels.crop_crop);
        btnReset.button({ disabled: !obj["crop"] });
    });

    return c;
};

})(jQuery);  