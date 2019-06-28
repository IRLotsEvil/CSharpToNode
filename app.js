const { app } = require("electron");
const _NET = require('net');

var args  = process.argv.slice(2); 
const _PIPE_ID = args.length > 0 ? args.shift() : null; // read argsv for the pipe id//

//// ELECTRON ////
app.once("ready", () => {
    if (_PIPE_ID !== null) {
        _NET.connect("\\\\.\\pipe\\" + _PIPE_ID, function () {
            console.log("Client connected to : " + _PIPE_ID);
        }).on("data", data => {
            var _TEMP_PIPE_ID = data.toString();
            if (_TEMP_PIPE_ID !== null && _TEMP_PIPE_ID !== "") {
                var temp = _NET.connect("\\\\.\\pipe\\" + _TEMP_PIPE_ID, function () {
                    console.log("Message Pipe Opened!! "+_TEMP_PIPE_ID);
                }).on("data", msg => {
                    if(msg){
                        //// DO SOMETHING HERE////
                        temp.write(msg); /// Send something back through the pipe// when it's finished writing, the c# process will close the temporary pipe///
                    }
                }).on("close", function () {
                    console.log("Message pipe closed "+_TEMP_PIPE_ID);
                });
            }
        }).on("close",function(){
            app.exit(); /// close application when host process disconnects//
        });
    }
});
