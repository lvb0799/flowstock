import { OrderDetailResponse, OrderKind } from '../types';
import { printOrder } from '../utils/print';

export default function OrderDetailModal({kind,data,onClose}:{kind:OrderKind;data:OrderDetailResponse;onClose:()=>void}){
  const sale=kind==='sales'; const h=data.header;
  const v=(obj:any,pascal:string,camel:string)=>obj?.[pascal] ?? obj?.[camel];
  return <div className="modal-mask" onMouseDown={e=>{if(e.target===e.currentTarget)onClose()}}><div className="modal order-detail-modal">
    <div className="modal-head"><div><h2>{sale?'銷貨單':'進貨單'} {v(h,'OrderNo','orderNo')}</h2><small>{v(h,'OrderDate','orderDate')}</small></div><div className="modal-head-actions"><button className="print-btn" onClick={()=>printOrder(kind,data)}>列印</button><button onClick={onClose}>×</button></div></div>
    <div className="order-detail-head">
      <div><span>{sale?'客戶':'供應商'}</span><b>{sale?v(h,'CustomerName','customerName'):v(h,'SupplierName','supplierName')}</b></div>
      <div><span>狀態</span><b>{v(h,'Status','status')}</b></div>
      {sale&&<div><span>付款狀態</span><b>{v(h,'PaymentStatus','paymentStatus')}</b></div>}
      <div><span>總金額</span><b>NT$ {Number(v(h,'TotalAmount','totalAmount')||0).toLocaleString()}</b></div>
    </div>
    <div className="order-lines-wrap"><table className="order-lines"><thead><tr><th>#</th><th>SKU</th><th>商品</th><th>數量</th><th>單價</th><th>小計</th></tr></thead><tbody>
      {data.items.map((x,i)=><tr key={v(x,'Id','id') ?? i}><td>{i+1}</td><td>{v(x,'Sku','sku')}</td><td>{v(x,'ProductName','productName')}</td><td>{v(x,'Qty','qty')} {v(x,'Unit','unit')}</td><td className="money">{Number(v(x,'UnitPrice','unitPrice')||0).toLocaleString()}</td><td className="money">{Number(v(x,'Amount','amount')||0).toLocaleString()}</td></tr>)}
    </tbody></table></div>
    {v(h,'Note','note')&&<div className="order-detail-note"><span>備註</span><p>{v(h,'Note','note')}</p></div>}
  </div></div>;
}
