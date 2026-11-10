const fs=require('node:fs'),path=require('node:path');
const source=path.join(__dirname,'src'),out=path.resolve(__dirname,'../Gateway/wwwroot');
fs.mkdirSync(out,{recursive:true});
for(const file of ['index.html','app.js','style.css']) fs.copyFileSync(path.join(source,file),path.join(out,file));
if(fs.existsSync(path.join(source,'assets'))) fs.cpSync(path.join(source,'assets'),path.join(out,'assets'),{recursive:true});
fs.copyFileSync(path.join(__dirname,'node_modules/angular/angular.min.js'),path.join(out,'angular.min.js'));
