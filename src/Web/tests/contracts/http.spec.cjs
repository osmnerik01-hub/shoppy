const {test,expect}=require('@playwright/test'),fs=require('node:fs'),YAML=require('yaml');
const catalog=YAML.parse(fs.readFileSync('../../contracts/http/catalog.openapi.yaml','utf8')),orders=YAML.parse(fs.readFileSync('../../contracts/http/orders.openapi.yaml','utf8'));
test('gerçek istemci istekleri OpenAPI sözleşmesine uyar',async({page})=>{let posted=false;await page.route('**/api/**',async r=>{const req=r.request(),url=new URL(req.url()),spec=url.pathname.includes('products')?catalog:orders;const path=Object.keys(spec.paths).find(p=>new RegExp('^/api'+p.replace('{id}','[^/]+')+'$').test(url.pathname));expect(path).toBeTruthy();const op=spec.paths[path][req.method().toLowerCase()];expect(op).toBeTruthy();if(req.method()==='POST'){posted=true;const body=req.postDataJSON(),schema=op.requestBody.content['application/json'].schema;expect(Object.keys(body).sort()).toEqual(schema.required.slice().sort());expect(body.quantity).toBeGreaterThanOrEqual(schema.properties.quantity.minimum);for(const p of op.parameters)if(p.required)expect(req.headers()[p.name.toLowerCase()]).toBeTruthy();}else for(const p of op.parameters||[])if(p.in==='query'){const raw=url.searchParams.get(p.name);if(raw===null){expect(p.required).not.toBe(true);continue;}if(p.schema.type==='string'){if(p.schema.enum)expect(p.schema.enum).toContain(raw);}else{const value=Number(raw);expect(Number.isFinite(value)).toBeTruthy();if(p.schema.minimum!==undefined)expect(value).toBeGreaterThanOrEqual(p.schema.minimum);if(p.schema.maximum!==undefined)expect(value).toBeLessThanOrEqual(p.schema.maximum);}}await r.fulfill({json:{items:[],totalCount:0}});});await page.goto('/');await page.evaluate(async()=>{const i=angular.element(document.body).injector();await i.get('catalogClient').list(1,{category:'Test category',brand:'Test brand',search:'Test',currency:'TRY',minPrice:0,maxPrice:100,sort:'price'});await i.get('catalogClient').get('11111111-1111-4111-8111-111111111111');await i.get('ordersClient').create('11111111-1111-4111-8111-111111111111',2,'contract-key');await i.get('ordersClient').get('22222222-2222-4222-8222-222222222222');});expect(posted).toBeTruthy();expect(orders.components.schemas.Order.required).toEqual(expect.arrayContaining(['id','quantity','unitPrice','currency','status']));expect(orders.components.schemas.Order.properties.status.enum).toEqual(['Pending','Confirmed','Rejected']);});

test('enriched catalog response contract',()=>{
 const schemas=catalog.components.schemas,p=schemas.Product;
 expect(p.required).toEqual(expect.arrayContaining(['id','name','description','price','currency','category','brand','images','specifications']));
 for(const key of ['category','brand']){expect(p.properties[key].nullable).toBe(true);expect(catalog.paths['/products'].get.parameters.find(x=>x.name===key).schema.maxLength).toBe(100);}
 expect(p.properties.images.items.$ref).toBe('#/components/schemas/ProductImage');
 expect(schemas.ProductImage.required).toEqual(['url','alt','position']);
 expect(schemas.ProductSpecification.required).toEqual(['name','value']);
});

test('catalog clients preserve enriched and legacy response fields',async({page})=>{
 const product={id:'11111111-1111-4111-8111-111111111111',name:'Contract fixture',description:'Test only',price:12.34,currency:'USD',category:'Electronics',brand:'Fixture',images:[{url:'https://example.com/front.png',alt:'Front',position:0},{url:'https://example.com/back.png',alt:'Back',position:2}],specifications:[{name:'Connection',value:'USB'}]};
 const legacy={...product,id:'22222222-2222-4222-8222-222222222222',category:null,brand:null,images:[],specifications:[]};
 for(const item of [product,legacy]){
  for(const field of catalog.components.schemas.Product.required)expect(item).toHaveProperty(field);
  for(const image of item.images){for(const field of catalog.components.schemas.ProductImage.required)expect(image).toHaveProperty(field);expect(image.url).toMatch(new RegExp(catalog.components.schemas.ProductImage.properties.url.pattern));}
  for(const spec of item.specifications)for(const field of catalog.components.schemas.ProductSpecification.required)expect(spec).toHaveProperty(field);
 }
 await page.route('**/api/products**',r=>r.fulfill({json:r.request().url().includes('/products/')?product:{items:[product,legacy],page:1,pageSize:12,totalCount:2}}));
 await page.goto('/');
 const data=await page.evaluate(async(id)=>{const c=angular.element(document.body).injector().get('catalogClient');return {list:await c.list(1),detail:await c.get(id)};},product.id);
 expect(data.list.items).toEqual([product,legacy]);expect(data.detail).toEqual(product);
});
