import { ArrowUpRight, BarChart3 } from 'lucide-react';
export default function Card({t,v,sub}:{t:string;v:any;sub?:string}){
  return <div className="metric-card">
    <div className="metric-top"><span>{t}</span><div className="metric-icon"><BarChart3 size={17}/></div></div>
    <strong>{v}</strong>
    <div className="metric-sub">{sub||'即時彙整'}<ArrowUpRight size={13}/></div>
  </div>;
}
