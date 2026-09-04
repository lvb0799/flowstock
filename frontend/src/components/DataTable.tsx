import { Pencil, Trash2, Inbox } from 'lucide-react';
import { Row } from '../types';

const labels:Record<string,string>={
  Id:'ID',Sku:'SKU',Code:'代碼',Name:'名稱',Unit:'單位',SalePrice:'售價',CostPrice:'平均成本',StockQty:'目前庫存',
  CreatedAt:'異動時間',ProductName:'商品',TxType:'異動類型',Qty:'數量',BeforeQty:'異動前',AfterQty:'異動後',ReferenceType:'來源',ReferenceId:'來源編號',
  OrderNo:'單號',OrderDate:'日期',TotalAmount:'總金額',Status:'狀態',CustomerName:'客戶',SupplierName:'供應商',BillNo:'帳單編號',
};
const moneyCols=new Set(['SalePrice','CostPrice','TotalAmount','Amount','PaidAmount','BalanceAmount','ReturnAmount','RefundDue','CostValue','SaleValue','GrossProfit']);
const statusText:Record<string,string>={UNPAID:'未付款',PARTIALLY_PAID:'部分付款',PAID:'已付款',OVERDUE:'已逾期',VOID:'已作廢',REFUND_DUE:'待退款',ISSUED:'已開立',DRAFT:'草稿',CONFIRMED:'已確認'};
function renderCell(c:string,v:any){
  if(c==='Status') return <span className={`status ${String(v||'').toLowerCase()}`}>{statusText[String(v)]||String(v??'')}</span>;
  if(moneyCols.has(c) && v!=='' && v!=null) return <span className="num">{Number(v).toLocaleString('zh-TW',{maximumFractionDigits:2})}</span>;
  if(c==='StockQty') return <span className={`stock-value ${Number(v)<10?'low':''}`}>{String(v??'')}</span>;
  return String(v??'');
}
export default function DataTable({rows,cols,onEdit,onDelete}:{rows:Row[];cols:string[];onEdit?:(r:Row)=>void;onDelete?:(r:Row)=>void}){
  const actions=!!onEdit||!!onDelete;
  return <div className="table-shell"><div className="tablewrap"><table><thead><tr>
    {cols.map(c=><th key={c}>{labels[c]||c}</th>)}{actions&&<th className="action-col">操作</th>}
  </tr></thead><tbody>
    {rows.map((r,i)=><tr key={r.Id??i}>
      {cols.map(c=><td key={c} className={moneyCols.has(c)?'money':''}>{renderCell(c,r[c])}</td>)}
      {actions&&<td className="actions">{onEdit&&<button className="btn-icon btn-edit" onClick={()=>onEdit(r)} title="修改"><Pencil size={15}/><span>修改</span></button>}{onDelete&&<button className="btn-icon btn-delete" onClick={()=>onDelete(r)} title="刪除"><Trash2 size={15}/><span>刪除</span></button>}</td>}
    </tr>)}
  </tbody></table></div>{rows.length===0&&<div className="empty-state"><div className="empty-icon"><Inbox size={22}/></div><b>目前沒有資料</b><span>新增資料後會顯示在這裡。</span></div>}</div>;
}
