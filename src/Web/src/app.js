(function () {
'use strict';
angular.module('shop', [])
.factory('catalogClient', function($http) { return {list:function(page,filters){return $http.get('/api/products',{params:angular.extend({page:page,pageSize:12},filters||{})}).then(function(r){return r.data;});},get:function(id){return $http.get('/api/products/'+encodeURIComponent(id)).then(function(r){return r.data;});}}; })
.factory('ordersClient', function($http) { return {create:function(id,quantity,key){return $http.post('/api/orders',{productId:id,quantity:quantity},{headers:{'Idempotency-Key':key}}).then(function(r){return r.data;});},get:function(id){return $http.get('/api/orders/'+encodeURIComponent(id)).then(function(r){return r.data;});}}; })
.controller('ShopController', function(catalogClient,ordersClient,$timeout,$scope,$window,$q) {
 var vm=this, timer, generation=0, request=0;
 vm.filters={}; vm.hasFilters=function(){return Object.keys(vm.filters).some(function(k){return vm.filters[k]!==undefined&&vm.filters[k]!==null&&vm.filters[k]!=='';});}; vm.change=function(){if(!vm.filters.currency){delete vm.filters.minPrice;delete vm.filters.maxPrice;if((vm.filters.sort||'').indexOf('price')===0)delete vm.filters.sort;}vm.load(1);};vm.clear=function(){vm.filters={};vm.load(1);};
 vm.view='catalog'; vm.items=[]; vm.cart=[]; vm.page=1; vm.results=[];
 vm.money=function(n,c){return new Intl.NumberFormat('tr-TR',{style:'currency',currency:c}).format(n);};
 vm.load=function(page){var token=++request;vm.items=[];vm.total=0;vm.view='catalog';vm.loading=true;vm.error='';vm.page=page;catalogClient.list(page,angular.copy(vm.filters)).then(function(data){if(token===request){vm.items=data.items;vm.total=data.totalCount;}},function(e){if(token!==request)return;vm.error=(e.data&&e.data.detail)||'Ürünlere bağlanılamadı. Yeniden deneyin.';}).finally(function(){if(token===request)vm.loading=false;});};
 vm.detail=function(p){var token=++request;vm.loading=true;vm.error='';vm.view='detail';vm.product=null;catalogClient.get(p.id).then(function(data){if(token===request)vm.product=data;},function(){if(token!==request)return;vm.error='Ürün ayrıntısı alınamadı. Ürün kaldırılmış olabilir.';}).finally(function(){if(token===request)vm.loading=false;});};
 vm.add=function(p){if(vm.locked)return;var line=vm.cart.find(function(x){return x.product.id===p.id;});if(line)line.quantity++;else vm.cart.push({product:p,quantity:1});vm.notice='Ürün sepete eklendi.';};
 vm.remove=function(line){if(!vm.locked)vm.cart.splice(vm.cart.indexOf(line),1);};
 vm.valid=function(){return vm.cart.length && vm.cart.every(function(x){return Number.isInteger(x.quantity)&&x.quantity>0&&x.quantity<=2147483647;});};
 vm.checkout=function(){if(vm.busy||!vm.valid())return;$timeout.cancel(timer);vm.busy=true;vm.locked=true;vm.error='';vm.view='result';var current=++generation;
 function poll(line){return ordersClient.get(line.order.id).then(function(order){line.order=order;if(order.status==='Pending')return;line.done=true;},function(){vm.error='Sipariş durumu alınamadı. Durumu yeniden sorgulayın.';});}
 var jobs=vm.cart.map(function(line){if(line.done)return $q.resolve();if(line.order)return poll(line);if(!line.key)line.key=$window.crypto.randomUUID();return ordersClient.create(line.product.id,line.quantity,line.key).then(function(order){line.order=order;return poll(line);},function(e){vm.error=e.status===409?'İşlem anahtarı uyuşmazlığı. Sepeti değiştirmeden yeniden deneyin.':'Sipariş yanıtı alınamadı. Aynı işlem güvenle yeniden sorgulanabilir.';});});
 $q.all(jobs).finally(function(){if(current!==generation)return;vm.busy=false;if(!vm.error&&vm.cart.some(function(x){return x.order&&x.order.status==='Pending';}))timer=$timeout(vm.checkout,1500);});
 };
 vm.canNewCart=function(){return !vm.busy&&vm.cart.length&&vm.cart.every(function(x){return x.done;});};
 vm.newCart=function(){if(!vm.canNewCart())return;vm.cart=[];vm.locked=false;vm.load(1);};
 $scope.$on('$destroy',function(){generation++;$timeout.cancel(timer);});vm.load(1);
});
})();