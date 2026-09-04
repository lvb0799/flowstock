import {
  LayoutDashboard, Package, Users, Truck, ShoppingCart, ReceiptText,
  RotateCcw, Warehouse, ClipboardCheck, WalletCards, FileText,
  BarChart3, History, Sun, Moon
} from 'lucide-react';
import { API } from '../api/client';
import brandMark from '../assets/lvb-flowstock-mark.png';

const items = [
  { group:'總覽', entries:[
    {key:'dashboard',label:'儀表板',icon:LayoutDashboard},
    {key:'reports',label:'營運報表',icon:BarChart3},
  ]},
  { group:'基礎資料', entries:[
    {key:'products',label:'商品管理',icon:Package},
    {key:'customers',label:'客戶管理',icon:Users},
    {key:'suppliers',label:'供應商管理',icon:Truck},
  ]},
  { group:'交易作業', entries:[
    {key:'purchases',label:'進貨作業',icon:ShoppingCart},
    {key:'sales',label:'銷貨作業',icon:ReceiptText},
    {key:'returns',label:'銷貨退貨',icon:RotateCcw},
  ]},
  { group:'庫存管理', entries:[
    {key:'inventory',label:'庫存異動',icon:Warehouse},
    {key:'counts',label:'庫存盤點',icon:ClipboardCheck},
  ]},
  { group:'財務與稽核', entries:[
    {key:'billing',label:'帳單 / 應收',icon:WalletCards},
    {key:'invoices',label:'發票管理',icon:FileText},
    {key:'history',label:'異動紀錄',icon:History},
  ]},
];

export const tabs=items.flatMap(g=>g.entries.map(e=>e.key));

export default function Sidebar({tab,onChange,theme,onToggleTheme}:{tab:string;onChange:(tab:string)=>void;theme:'light'|'dark';onToggleTheme:()=>void}){
  return <aside className="sidebar">
    <div className="brand">
      <div className="brand-mark"><img src={brandMark} alt="LVB FlowStock" /></div>
      <div className="brand-copy"><strong>LVB FlowStock</strong><span>進銷存與營運管理系統</span></div>
      <button className="theme-toggle" type="button" onClick={onToggleTheme} aria-label={theme==='dark'?'切換亮色主題':'切換暗色主題'} title={theme==='dark'?'切換亮色主題':'切換暗色主題'}>
        {theme==='dark'?<Sun size={17}/>:<Moon size={17}/>}
      </button>
    </div>
    <nav className="sidebar-nav" aria-label="主要功能">
      {items.map(group=><div className="nav-group" key={group.group}>
        <div className="nav-group-title">{group.group}</div>
        {group.entries.map(({key,label,icon:Icon})=><button className={`nav-item ${tab===key?'active':''}`} onClick={()=>onChange(key)} key={key}>
          <Icon size={18} strokeWidth={1.9}/><span>{label}</span>
        </button>)}
      </div>)}
    </nav>
    <div className="sidebar-footer">
      <div className="system-status"><span className="system-dot"></span><div><b>系統連線正常</b><small>{API}</small></div></div>
      <span className="version-pill">MVP v1.0.2</span>
    </div>
  </aside>
}
