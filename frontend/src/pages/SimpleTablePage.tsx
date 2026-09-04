import DataTable from '../components/DataTable'; import { Row } from '../types';
export default function SimpleTablePage({title,rows,cols}:{title:string;rows:Row[];cols:string[]}){return <><h1>{title}</h1><DataTable rows={rows} cols={cols}/></>}
