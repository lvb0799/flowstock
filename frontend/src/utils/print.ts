import { Row } from '../types';
import { money } from './format';

const esc=(v:any)=>String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]||m));
const company=()=>localStorage.getItem('companyName')||'公司名稱';

export function printHtml(title:string,body:string){
 const w=window.open('','_blank','width=980,height=760'); if(!w){alert('瀏覽器阻擋了列印視窗，請允許彈出視窗');return;}
 w.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>${esc(title)}</title><style>
 body{font-family:Arial,"Microsoft JhengHei",sans-serif;color:#111;padding:28px}h1{font-size:22px;margin:0 0 4px}.company{font-size:18px;font-weight:700}.muted{color:#666;font-size:12px}.head{display:flex;justify-content:space-between;border-bottom:2px solid #111;padding-bottom:14px;margin-bottom:16px}.meta{display:grid;grid-template-columns:1fr 1fr;gap:7px 30px;margin:14px 0}table{width:100%;border-collapse:collapse;margin-top:14px}th,td{border-bottom:1px solid #ccc;padding:8px;text-align:left}th{background:#f3f4f6}.right{text-align:right}.total{margin-top:18px;margin-left:auto;width:320px}.total div{display:flex;justify-content:space-between;padding:5px}.grand{font-size:18px;font-weight:700;border-top:2px solid #111}.note{margin-top:20px;padding:12px;background:#f7f7f7}.toolbar{margin-bottom:18px}@media print{.toolbar{display:none}body{padding:0}@page{margin:14mm}}</style></head><body><div class="toolbar"><button onclick="window.print()">列印 / 另存 PDF</button></div>${body}</body></html>`);
 w.document.close(); w.focus();
}

export function printOrder(kind:'sales'|'purchases',data:any){const h=data.header;const sale=kind==='sales';const title=`${sale?'銷貨單':'進貨單'} ${h.OrderNo}`;printHtml(title,`
<div class="head"><div><div class="company">${esc(company())}</div><h1>${sale?'銷貨單':'進貨單'}</h1></div><div><b>${esc(h.OrderNo)}</b><div class="muted">${esc(h.OrderDate)}</div></div></div>
<div class="meta"><div>${sale?'客戶':'供應商'}：<b>${esc(sale?h.CustomerName:h.SupplierName)}</b></div><div>狀態：${esc(h.Status)}</div>${sale?`<div>付款狀態：${esc(h.PaymentStatus)}</div>`:''}</div>
<table><thead><tr><th>#</th><th>SKU</th><th>商品</th><th class="right">數量</th><th class="right">單價</th><th class="right">小計</th></tr></thead><tbody>${data.items.map((x:Row,i:number)=>`<tr><td>${i+1}</td><td>${esc(x.Sku)}</td><td>${esc(x.ProductName)}</td><td class="right">${esc(x.Qty)} ${esc(x.Unit)}</td><td class="right">${money(x.UnitPrice)}</td><td class="right">${money(x.Amount)}</td></tr>`).join('')}</tbody></table>
<div class="total"><div class="grand"><span>總金額</span><span>NT$ ${money(h.TotalAmount)}</span></div></div>${h.Note?`<div class="note">備註：${esc(h.Note)}</div>`:''}`)}

export function printBilling(data:any){const b=data.billing;const net=(Number(b.NetAmount)>0||Number(b.ReturnAmount)>0)?b.NetAmount:b.Amount;printHtml(`帳單 ${b.BillNo}`,`
<div class="head"><div><div class="company">${esc(company())}</div><h1>帳單 / 請款單</h1></div><div><b>${esc(b.BillNo)}</b><div class="muted">${esc(b.BillDate)}</div></div></div>
<div class="meta"><div>客戶：<b>${esc(b.CustomerName)}</b></div><div>客戶代碼：${esc(b.CustomerCode||'-')}</div><div>銷貨單：${esc(b.OrderNo)}</div><div>付款期限：${esc(b.DueDate||'-')}</div><div>Email：${esc(b.CustomerEmail||'-')}</div><div>地址：${esc(b.CustomerAddress||'-')}</div></div>
<table><thead><tr><th>#</th><th>SKU</th><th>商品</th><th class="right">數量</th><th class="right">單價</th><th class="right">小計</th></tr></thead><tbody>${(data.items||[]).map((x:Row,i:number)=>`<tr><td>${i+1}</td><td>${esc(x.Sku)}</td><td>${esc(x.ProductName)}</td><td class="right">${esc(x.Qty)} ${esc(x.Unit)}</td><td class="right">${money(x.UnitPrice)}</td><td class="right">${money(x.Amount)}</td></tr>`).join('')}</tbody></table>
<div class="total"><div><span>原始金額</span><span>${money(b.Amount)}</span></div><div><span>退貨</span><span>- ${money(b.ReturnAmount||0)}</span></div><div><span>淨應收</span><span>${money(net)}</span></div><div><span>已收款</span><span>${money(b.PaidAmount||0)}</span></div><div><span>已退款</span><span>${money(b.RefundedAmount||0)}</span></div><div class="grand"><span>尚未收款</span><span>NT$ ${money(b.BalanceAmount||0)}</span></div></div>`)}

export function printReturn(data:any){const h=data.header;printHtml(`退貨單 ${h.ReturnNo}`,`
<div class="head"><div><div class="company">${esc(company())}</div><h1>銷貨退貨單</h1></div><div><b>${esc(h.ReturnNo)}</b><div class="muted">${esc(h.ReturnDate)}</div></div></div><div class="meta"><div>客戶：<b>${esc(h.CustomerName)}</b></div><div>原銷貨單：${esc(h.OrderNo)}</div></div>
<table><thead><tr><th>#</th><th>SKU</th><th>商品</th><th class="right">退貨量</th><th class="right">單價</th><th class="right">小計</th></tr></thead><tbody>${data.items.map((x:Row,i:number)=>`<tr><td>${i+1}</td><td>${esc(x.Sku)}</td><td>${esc(x.ProductName)}</td><td class="right">${esc(x.Qty)} ${esc(x.Unit)}</td><td class="right">${money(x.UnitPrice)}</td><td class="right">${money(x.Amount)}</td></tr>`).join('')}</tbody></table><div class="total"><div class="grand"><span>退貨總額</span><span>NT$ ${money(h.TotalAmount)}</span></div></div>${h.Reason?`<div class="note">退貨原因：${esc(h.Reason)}</div>`:''}`)}
