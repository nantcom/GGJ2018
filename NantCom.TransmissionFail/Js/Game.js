(function () {

    var app = angular.module("transmissionfail", ["ngRoute"]);

    app.config(function ($routeProvider) {
        $routeProvider
            .when("/", {
                templateUrl: "/Content/scene_selectgame.html"
            })
            .when("/lobby", {
                templateUrl: "/Content/scene_lobby.html"
            })
            .when("/Spy", {
                templateUrl: "/Content/scene_spy.html"
            })
            .when("/Operator", {
                templateUrl: "/Content/scene_operator.html"
            })
            .when("/Handler", {
                templateUrl: "/Content/scene_handler.html"
            })
            .when("/pass", {
                templateUrl: "/Content/scene_pass.html"
            })
            .when("/fail", {
                templateUrl: "/Content/scene_fail.html"
            });
    });

    app.controller("TransmissionGame", function ($scope, $location) {

        var $me = this;

        $scope.globalState = {};

        $scope.globalState.isConnected = false;
        $scope.globalState.currentMatch = "no match";
        $scope.globalState.referencePicture = "https://globalgamejam.org/sites/default/files/styles/feature_image__normal/public/site/img_0054_0.jpg?itok=mRKt_BYN&timestamp=1506078896 282w";
        
        var connection = $.hubConnection();
        $scope.hub = connection.createHubProxy('failHub');

        connection.start().done(function () {

            console.log("connected.");

            $scope.$apply(function () {
                $scope.globalState.isConnected = true;

            });

        });

        $scope.hub.on('gameEnd', function (result) {

            $scope.$apply(function () {

                $scope.globalState.result = result;

                if (result.TimeOut == true || result.Result == false) {
                    $location.path('/fail');
                }

                if (result.Result == true) {
                    $location.path('/pass');
                }
            });

        });
    });

    app.controller("SelectGameScene", function ($scope, $location) {

        var $me = this;
        $me.refreshGames = function () {

            $scope.hub.invoke('listMatch').done(function (data) {
                
                $scope.$apply(function () {

                    $scope.gamelist = data;

                });

            }).fail(function (error) {
                console.log('Cannot List Match: ' + error);
            });
        };

        $me.newGame = function (name) {

            $scope.hub.invoke('createMatch', name).done(function (gameId) {

                console.log('Create Game: ' + gameId);
                $me.joinGame(gameId);

            }).fail(function (error) {
                console.log('Cannot Create Game: ' + error);
            });
        };

        $me.joinGame = function (match) {

            var date = new Date();

            $scope.hub.invoke('joinMatch', match, 0, { name : 'Player' + date.getMilliseconds() }).done(function (data) {

                console.log("Joined Game: " + match);

                $scope.$apply(function () {

                    $scope.globalState.currentMatch = match;
                    $location.path('/lobby');

                });

            }).fail(function (error) {
                console.log('Cannot Join Match: ' + error);
            });
        };

        $scope.gamelist = [];
        $scope.$watch("globalState.isConnected", function (value) {

            if (value === true) {

                $me.refreshGames();

            } else {
                $scope.gamelist = [];
            }

        });


        $scope.hub.on('newMatchCreated', function (list) {

            $scope.$apply(function () {

                $scope.gamelist = list;

            });
        });

    });

    app.controller("LobbyScene", function ($scope, $location) {

        $scope.alerts = [];

        $scope.hub.on('playerJoin', function (playerInfo) {

            $scope.$apply(function () {
                $scope.alerts.push({

                    message:  playerInfo.name + ' has joined your team'
                });
            });
        });
        
        $scope.hub.on('gameStartNotify', function (startTime) {

            var countdown = 5;
            var notifyTimer = null;
            notifyTimer = window.setInterval(function () {

                $scope.$apply(function () {
                    $scope.alerts.push({
                        message: 'Game will start in ' + countdown + ' seconds!'
                    });
                });
                countdown--;

                if (countdown == 0) {
                    window.clearInterval(notifyTimer);
                }
            }, 1000);
        });

        $scope.hub.on('gameStart', function (parameter) {

            console.log("Game Started: " + parameter);

            $scope.$apply(function () {
                
                $scope.globalState.parameter = parameter;
                $location.path('/' + parameter.Role);
            });

        });
    });    
    

    app.controller("SpyScene", function ($scope, $location) {
        
    });    

    app.controller("OperatorScene", function ($scope, $location) {

    });    

    app.controller("HandlerScene", function ($scope, $location) {

        var $me = this;
        $me.vote = function (index) {

            $scope.hub.invoke('submitVote', $scope.globalState.currentMatch, index).done(function (data) {
                

            }).fail(function (error) {
                console.log('Cannot Vote: ' + error);
            });
        };

    });  

    app.controller("PassScene", function ($scope, $location) {

    });    


    app.controller("FailScene", function ($scope, $location) {

    });  

    app.controller("DrawingPanel", function ($scope, $location) {

    }); 

    app.controller("ViewerPanel", function ($scope, $location) {

    }); 
})();