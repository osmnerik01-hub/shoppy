(function () {
'use strict';
angular.module('shop', [])
.factory('catalogClient', function($http) { return {list:function(page,filters){return $http.get('/api/products',{params:angular.extend({page:page,pageSize:12},filters||{})}).then(function(r){return r.data;});},get:function(id){return $http.get('/api/products/'+encodeURIComponent(id)).then(function(r){return r.data;});}}; })
.factory('ordersClient', function($http) { return {create:function(id,quantity,key){return $http.post('/api/orders',{productId:id,quantity:quantity},{headers:{'Idempotency-Key':key}}).then(function(r){return r.data;});},get:function(id){return $http.get('/api/orders/'+encodeURIComponent(id)).then(function(r){return r.data;});}}; })
.directive('imageFallback', function(){return {link:function(scope,element){var fallback=angular.element('<span class="image-missing" role="img" aria-label="Görsel kullanılamıyor">Görsel kullanılamıyor</span>');element.after(fallback);fallback.css('display','none');element.on('error',function(){element.css('display','none');fallback.css('display','grid');});element.on('load',function(){element.css('display','');fallback.css('display','none');});scope.$on('$destroy',function(){element.off('error load');fallback.remove();});}};})
.controller('ShopController', function(catalogClient,ordersClient,$timeout,$scope,$window,$q) {
 var vm=this, timer, generation=0, request=0;
 var storageKey='vera.shopping.v1', saved={};
 try {saved=JSON.parse($window.localStorage.getItem(storageKey)||'{}')||{};}catch(e){vm.storageWarning=true;}
 vm.persist=function(){try{$window.localStorage.setItem(storageKey,JSON.stringify({cart:vm.cart,favorites:vm.favorites,locked:vm.locked}));}catch(e){vm.storageWarning=true;}};
 vm.favorites=Array.isArray(saved.favorites)?saved.favorites.filter(function(p){return p&&p.id;}):[];
 vm.favorite=function(p){return vm.favorites.some(function(x){return x.id===p.id;});};
 vm.toggleFavorite=function(p){var i=vm.favorites.findIndex(function(x){return x.id===p.id;});if(i<0)vm.favorites.push(angular.copy(p));else vm.favorites.splice(i,1);vm.persist();};
 vm.openFavorites=function(){++request;vm.loading=false;vm.error='';vm.notice='';vm.view='favorites';};
 vm.totals=function(){var totals={};vm.cart.forEach(function(l){if(!l.unavailable&&Number.isInteger(l.quantity)&&l.quantity>0)totals[l.product.currency]=(totals[l.product.currency]||0)+l.product.price*l.quantity;});return totals;};
 vm.refreshCart=function(){vm.refreshing=true;return $q.all(vm.cart.map(function(line){return catalogClient.get(line.product.id).then(function(p){line.priceChanged=p.price!==line.product.price||p.currency!==line.product.currency;line.product=p;line.unavailable=false;line.unverified=false;},function(e){line.unavailable=e.status===404;line.unverified=!line.unavailable;});})).finally(function(){vm.refreshing=false;vm.persist();});};
 vm.adjust=function(line,n){if(!vm.locked){line.quantity=Math.max(1,(line.quantity||1)+n);vm.persist();}};
 vm.categories=[];vm.filtersOpen=false;
 vm.image=function(p){return (p.images||[]).slice().sort(function(a,b){return a.position-b.position;})[0];};
 vm.category=function(c){vm.filters.category=c;vm.change();};
 vm.filterLabel=function(k){return {search:'Arama',category:'Kategori',brand:'Marka',currency:'Para birimi',minPrice:'En düşük',maxPrice:'En yüksek',sort:'Sıralama'}[k]||k;};
 vm.removeFilter=function(k){delete vm.filters[k];vm.change();};
 vm.openCart=function(){++request;vm.loading=false;vm.error='';vm.view='cart';vm.notice='';if(!vm.locked)vm.refreshCart();};
 vm.openResult=function(){++request;vm.loading=false;vm.error='';vm.view='result';};
 vm.filters={}; vm.hasFilters=function(){return Object.keys(vm.filters).some(function(k){return vm.filters[k]!==undefined&&vm.filters[k]!==null&&vm.filters[k]!=='';});}; vm.change=function(){if(!vm.filters.currency){delete vm.filters.minPrice;delete vm.filters.maxPrice;if((vm.filters.sort||'').indexOf('price')===0)delete vm.filters.sort;}vm.load(1);};vm.clear=function(){vm.filters={};vm.load(1);};
 vm.view='catalog'; vm.items=[]; vm.cart=Array.isArray(saved.cart)?saved.cart.filter(function(l){return l&&l.product&&l.product.id;}):[];vm.locked=!!saved.locked||vm.cart.some(function(l){return l.key||l.order;}); vm.page=1; vm.results=[];
 vm.money=function(n,c){return new Intl.NumberFormat('tr-TR',{style:'currency',currency:c}).format(n);};
 vm.load=function(page){var token=++request;vm.items=[];vm.total=0;vm.view='catalog';vm.loading=true;vm.error='';vm.page=page;catalogClient.list(page,angular.copy(vm.filters)).then(function(data){if(token===request){vm.items=data.items;vm.total=data.totalCount;data.items.forEach(function(p){if(p.category&&vm.categories.indexOf(p.category)<0)vm.categories.push(p.category);});}},function(e){if(token!==request)return;vm.error=(e.data&&e.data.detail)||'Ürünlere bağlanılamadı. Yeniden deneyin.';}).finally(function(){if(token===request)vm.loading=false;});};
 vm.detail=function(p){var token=++request;vm.loading=true;vm.error='';vm.view='detail';vm.product=null;vm.lastProduct=p;vm.notice='';catalogClient.get(p.id).then(function(data){if(token===request){vm.product=data;vm.selectedImage=vm.image(data);}},function(){if(token!==request)return;vm.error='Ürün ayrıntısı alınamadı. Ürün kaldırılmış olabilir.';}).finally(function(){if(token===request)vm.loading=false;});};
 vm.add=function(p){if(vm.locked)return;var line=vm.cart.find(function(x){return x.product.id===p.id;});if(line)line.quantity++;else vm.cart.push({product:p,quantity:1});vm.persist();vm.notice='Ürün sepete eklendi.';};
 vm.remove=function(line){if(!vm.locked){vm.cart.splice(vm.cart.indexOf(line),1);vm.persist();}};
 vm.valid=function(){return vm.cart.length && vm.cart.every(function(x){return !x.unavailable&&!x.unverified&&Number.isInteger(x.quantity)&&x.quantity>0&&x.quantity<=2147483647;});};
 vm.checkout=function(){if(vm.busy||vm.refreshing||!vm.valid())return;$timeout.cancel(timer);vm.busy=true;vm.locked=true;vm.error='';vm.view='result';var current=++generation;vm.persist();
 function poll(line){return ordersClient.get(line.order.id).then(function(order){line.order=order;vm.persist();if(order.status==='Pending')return;line.done=true;vm.persist();},function(){vm.error='Sipariş durumu alınamadı. Durumu yeniden sorgulayın.';});}
 var jobs=vm.cart.map(function(line){if(line.done)return $q.resolve();if(line.order)return poll(line);if(!line.key)line.key=$window.crypto.randomUUID();vm.persist();return ordersClient.create(line.product.id,line.quantity,line.key).then(function(order){line.order=order;vm.persist();return poll(line);},function(e){if(e.status===404){line.failure='Ürün kaldırılmış. Bu ürün için sipariş oluşturulmadı.';line.done=true;vm.persist();return;}vm.error=e.status===409?'İşlem anahtarı uyuşmazlığı. Sepeti değiştirmeden yeniden deneyin.':'Sipariş yanıtı alınamadı. Aynı işlem güvenle yeniden sorgulanabilir.';});});
 $q.all(jobs).finally(function(){if(current!==generation)return;vm.busy=false;if(!vm.error&&vm.cart.some(function(x){return x.order&&x.order.status==='Pending';}))timer=$timeout(vm.checkout,1500);});
 };
 vm.canNewCart=function(){return !vm.busy&&vm.cart.length&&vm.cart.every(function(x){return x.done;});};
 vm.newCart=function(){if(!vm.canNewCart())return;vm.cart=[];vm.locked=false;vm.persist();vm.load(1);};
 $scope.$on('$destroy',function(){generation++;$timeout.cancel(timer);});$scope.$watch(function(){return vm.cart.map(function(l){return l.quantity;}).join(',');},function(){vm.persist();});if(vm.locked){vm.view='result';vm.checkout();}else vm.load(1);
});
})();
