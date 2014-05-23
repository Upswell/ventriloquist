(function($){
	$.extend({
		websocketSettings: {
			open: function(){},
			close: function(){},
			message: function(){},
			options: {},
			events: {}
		},
		websocket: function(url, s) {
			var ws = WebSocket ? new WebSocket( url ) : {
				send: function(m){ return false },
				close: function(){}
			};
			$.websocketSettings = $.extend($.websocketSettings, s);
			$(ws)
				.bind('open', $.websocketSettings.open)
				.bind('close', $.websocketSettings.close)
				.bind('message', $.websocketSettings.message)
				.bind('message', function(e){
					var m = jQuery.parseJSON(e.originalEvent.data);
					var h = $.websocketSettings.events[m.event];
					if (h) h.call(this, m);
				});
			ws._settings = $.extend($.websocketSettings, s);
			ws._send = ws.send;
			ws.send = function(data) {
				return this._send(data);
			}
			$(window).unload(function(){ ws.close(); ws = null });
			return ws;
		}
	});
})(jQuery);