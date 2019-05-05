"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/serverHub").build();

connection.on("dataChanged", function (payload) {
    var status = document.getElementById("status");
    var players = document.getElementById("players");
    var playerCount = document.getElementById("playerCount");
    var loggedIn = document.getElementById("loggedIn");
    if (payload == undefined || payload == null) {
        status.textContent = "offline";
        playerCount.textContent = "n/a";
        loggedIn.hidden = true;
    } else {
        status.textContent = "online";
        var online = payload.players.online;
        var max = payload.players.max;
        playerCount.textContent = online + "/" + max;

        var list = "";
        if (online > 0) {
            loggedIn.hidden = false;
            for (var i = 0; i < online; i++) {
                list += "<li>" + payload.players.sample[i].name + "</li>";
            }
        } else {
            loggedIn.hidden = true;
        }
        players.innerHTML = list;
    }
    
});

connection.start().then(() => update());
setInterval(update, 1000 * 10);

function update() {
    connection.invoke("UpdateServerStatus").catch((err) => console.error(err.toString()));
}
