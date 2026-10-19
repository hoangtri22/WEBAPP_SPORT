import { useMemo, useState, type ReactNode } from "react";
import {
  Activity,
  AlertCircle,
  ArrowLeft,
  BadgePercent,
  BarChart3,
  Bell,
  Boxes,
  CalendarClock,
  Check,
  CheckCircle2,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleDollarSign,
  ClipboardList,
  ClipboardCheck,
  CreditCard,
  Download,
  Edit3,
  Eye,
  EyeOff,
  FileText,
  Heart,
  Grid2X2,
  Headphones,
  LayoutDashboard,
  List,
  Lock,
  LogOut,
  Mail,
  MapPin,
  Menu,
  MessageSquareText,
  MoreHorizontal,
  Package,
  PackageOpen,
  Plus,
  Printer,
  Search,
  Send,
  Settings2,
  ShieldCheck,
  ShoppingBag,
  ShoppingCart,
  Sparkles,
  Star,
  Store,
  Truck,
  Trash2,
  TrendingUp,
  UserRound,
  UserPlus,
  Users,
  Zap,
  X,
  type LucideIcon,
} from "lucide-react";

type PageId =
  | "overview"
  | "accounts"
  | "customers"
  | "employees"
  | "categories"
  | "products"
  | "invoices"
  | "pos"
  | "promotions"
  | "feedback"
  | "surveys"
  | "imports"
  | "reports";

type StatusTone = "warning" | "info" | "success" | "danger" | "neutral";

const menuItems: { id: PageId; label: string; icon: LucideIcon }[] = [
  { id: "overview", label: "Tổng quan", icon: LayoutDashboard },
  { id: "accounts", label: "Tài khoản & Phân quyền", icon: ShieldCheck },
  { id: "customers", label: "Quản lý khách hàng", icon: Users },
  { id: "employees", label: "Quản lý nhân viên", icon: UserRound },
  { id: "categories", label: "Quản lý loại sản phẩm", icon: Boxes },
  { id: "products", label: "Quản lý sản phẩm", icon: Package },
  { id: "invoices", label: "Quản lý hóa đơn", icon: FileText },
  { id: "pos", label: "Bán hàng tại quầy", icon: ShoppingCart },
  { id: "promotions", label: "Khuyến mãi & Marketing", icon: BadgePercent },
  { id: "feedback", label: "Phản hồi khách hàng", icon: MessageSquareText },
  { id: "surveys", label: "Khảo sát", icon: ClipboardList },
  { id: "imports", label: "Quản lý nhập hàng", icon: PackageOpen },
  { id: "reports", label: "Báo cáo thống kê", icon: BarChart3 },
];

const pageMeta: Record<PageId, { title: string; description: string }> = {
  overview: { title: "Tổng quan hệ thống", description: "Theo dõi hoạt động kinh doanh hôm nay" },
  accounts: { title: "Tài khoản & Phân quyền", description: "Quản lý quyền truy cập hệ thống" },
  customers: { title: "Quản lý khách hàng", description: "Thông tin và lịch sử mua hàng của khách hàng" },
  employees: { title: "Quản lý nhân viên", description: "Theo dõi đội ngũ và trạng thái làm việc" },
  categories: { title: "Quản lý loại sản phẩm", description: "Sắp xếp danh mục hàng hóa" },
  products: { title: "Quản lý sản phẩm", description: "Quản lý danh sách và biến thể sản phẩm" },
  invoices: { title: "Quản lý hóa đơn", description: "Tra cứu và xử lý đơn hàng" },
  pos: { title: "Bán hàng tại quầy", description: "Tạo hóa đơn nhanh cho khách tại cửa hàng" },
  promotions: { title: "Khuyến mãi & Marketing", description: "Tạo và theo dõi các chiến dịch ưu đãi" },
  feedback: { title: "Phản hồi khách hàng", description: "Lắng nghe và hỗ trợ khách hàng" },
  surveys: { title: "Khảo sát", description: "Thu thập ý kiến và xem thống kê kết quả" },
  imports: { title: "Quản lý nhập hàng", description: "Theo dõi phiếu nhập và nhà cung cấp" },
  reports: { title: "Báo cáo thống kê", description: "Phân tích hiệu quả kinh doanh" },
};

const recentOrders = [
  { id: "HD01284", name: "Nguyễn Minh Anh", date: "24/05/2025 · 09:32", total: "2.450.000₫", status: "Chờ xác nhận", tone: "warning" as StatusTone },
  { id: "HD01283", name: "Trần Quốc Huy", date: "24/05/2025 · 09:14", total: "1.280.000₫", status: "Đang chuẩn bị", tone: "info" as StatusTone },
  { id: "HD01282", name: "Lê Ngọc Mai", date: "24/05/2025 · 08:40", total: "3.890.000₫", status: "Hoàn thành", tone: "success" as StatusTone },
  { id: "HD01281", name: "Phạm Gia Bảo", date: "23/05/2025 · 20:16", total: "890.000₫", status: "Hoàn thành", tone: "success" as StatusTone },
  { id: "HD01280", name: "Hoàng Thanh Hà", date: "23/05/2025 · 19:52", total: "1.650.000₫", status: "Đã hủy", tone: "danger" as StatusTone },
];

const products = [
  { name: "Nike Air Zoom Pegasus 41", category: "Giày chạy bộ", price: "3.290.000₫", stock: 36, color: "#dceee9", emoji: "N" },
  { name: "Áo Adidas Own The Run", category: "Áo thể thao", price: "990.000₫", stock: 58, color: "#fff0df", emoji: "A" },
  { name: "Vợt cầu lông Yonex Astrox", category: "Cầu lông", price: "2.450.000₫", stock: 12, color: "#e7eafe", emoji: "Y" },
  { name: "Bóng đá Molten F5A", category: "Bóng đá", price: "680.000₫", stock: 24, color: "#fce8ec", emoji: "M" },
  { name: "Quần Puma Training", category: "Quần thể thao", price: "820.000₫", stock: 41, color: "#e2effe", emoji: "P" },
  { name: "Túi Nike Brasilia 9.5", category: "Phụ kiện", price: "1.190.000₫", stock: 19, color: "#fef3d5", emoji: "N" },
];

function Logo({ compact = false }: { compact?: boolean }) {
  return (
    <div className="flex items-center gap-3">
      <div className="logo-mark"><Activity size={21} strokeWidth={2.5} /></div>
      {!compact && <span className="text-[19px] font-extrabold tracking-[-0.04em] text-slate-900">SPORT<span className="text-teal-700">SHOP</span></span>}
    </div>
  );
}

function Button({
  children,
  variant = "primary",
  onClick,
  className = "",
  type = "button",
}: {
  children: ReactNode;
  variant?: "primary" | "secondary" | "ghost" | "orange";
  onClick?: () => void;
  className?: string;
  type?: "button" | "submit";
}) {
  return <button type={type} onClick={onClick} className={`btn btn-${variant} ${className}`}>{children}</button>;
}

function StatusBadge({ children, tone = "neutral" }: { children: ReactNode; tone?: StatusTone }) {
  return <span className={`status status-${tone}`}><span className="status-dot" />{children}</span>;
}

function IconButton({ icon: Icon, label }: { icon: LucideIcon; label: string }) {
  return <button className="icon-btn" aria-label={label} title={label}><Icon size={17} /></button>;
}

function SearchInput({ placeholder = "Tìm kiếm..." }: { placeholder?: string }) {
  return (
    <label className="search-box">
      <Search size={18} />
      <input placeholder={placeholder} />
      <kbd>⌘ K</kbd>
    </label>
  );
}

function PageHeading({ page, action }: { page: PageId; action?: ReactNode }) {
  const meta = pageMeta[page];
  return (
    <div className="page-heading">
      <div>
        <p className="eyebrow">SPORTSHOP ADMIN</p>
        <h1>{meta.title}</h1>
        <p>{meta.description}</p>
      </div>
      {action}
    </div>
  );
}

function Sidebar({ active, onChange, collapsed, onToggle }: { active: PageId; onChange: (id: PageId) => void; collapsed: boolean; onToggle: () => void }) {
  return (
    <aside className={`sidebar ${collapsed ? "sidebar-collapsed" : ""}`}>
      <div className="sidebar-top">
        <Logo compact={collapsed} />
        <button className="collapse-btn" onClick={onToggle} aria-label="Thu gọn menu"><Menu size={19} /></button>
      </div>
      <nav>
        {menuItems.map(({ id, label, icon: Icon }) => (
          <button key={id} className={`nav-item ${active === id ? "active" : ""}`} onClick={() => onChange(id)} title={collapsed ? label : undefined}>
            <Icon size={19} />
            {!collapsed && <span>{label}</span>}
            {id === "feedback" && !collapsed && <span className="nav-badge">5</span>}
          </button>
        ))}
      </nav>
      <div className="sidebar-help">
        <div className="help-icon"><Headphones size={18} /></div>
        {!collapsed && <div><strong>Cần hỗ trợ?</strong><span>support@sportshop.vn</span></div>}
      </div>
    </aside>
  );
}

function Header({ onLogout, onMenu, onStore }: { onLogout: () => void; onMenu: () => void; onStore: () => void }) {
  return (
    <header className="header">
      <div className="welcome">
        <button className="mobile-menu" onClick={onMenu}><Menu size={20} /></button>
        <div><span>Chào mừng trở lại,</span><strong>Quản lý Minh</strong></div>
      </div>
      <div className="header-actions">
        <Button variant="secondary" onClick={onStore}><ShoppingBag size={17} />Cửa hàng</Button>
        <button className="notification"><Bell size={19} /><span /></button>
        <div className="profile">
          <div className="avatar">MH</div>
          <div><strong>Minh Hoàng</strong><span>QL000</span></div>
          <ChevronDown size={16} />
        </div>
        <Button variant="secondary" onClick={onLogout}><LogOut size={17} />Đăng xuất</Button>
      </div>
    </header>
  );
}

function StatCard({ icon: Icon, value, label, change, tone }: { icon: LucideIcon; value: string; label: string; change: string; tone: string }) {
  return (
    <div className="card stat-card">
      <div className={`stat-icon ${tone}`}><Icon size={23} /></div>
      <div className="stat-body"><span>{label}</span><strong>{value}</strong><small><TrendingUp size={13} />{change}</small></div>
      <MoreHorizontal className="stat-more" size={19} />
    </div>
  );
}

function OrdersTable({ compact = false }: { compact?: boolean }) {
  return (
    <div className="table-wrap">
      <table>
        <thead><tr><th>Mã HĐ</th><th>Khách hàng</th><th>Ngày lập</th><th>Tổng tiền</th><th>Trạng thái</th><th /></tr></thead>
        <tbody>
          {recentOrders.slice(0, compact ? 4 : 5).map((order) => (
            <tr key={order.id}>
              <td><strong className="order-code">{order.id}</strong></td>
              <td><div className="customer-cell"><span>{order.name.charAt(0)}</span><strong>{order.name}</strong></div></td>
              <td className="muted">{order.date}</td><td><strong>{order.total}</strong></td>
              <td><StatusBadge tone={order.tone}>{order.status}</StatusBadge></td>
              <td><IconButton icon={Eye} label="Xem hóa đơn" /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Overview({ onNavigate }: { onNavigate: (id: PageId) => void }) {
  const stats = [
    { icon: CircleDollarSign, value: "48,6 tr", label: "Doanh thu hôm nay", change: "+12,5% so với hôm qua", tone: "teal" },
    { icon: ShoppingBag, value: "128", label: "Đơn hàng mới", change: "+8,2% so với hôm qua", tone: "orange" },
    { icon: UserRound, value: "24", label: "Nhân viên", change: "22 đang làm việc", tone: "blue" },
    { icon: Users, value: "3.842", label: "Khách hàng", change: "+42 khách hàng mới", tone: "purple" },
    { icon: MessageSquareText, value: "05", label: "Phản hồi chưa trả lời", change: "Cần xử lý sớm", tone: "pink" },
    { icon: CalendarClock, value: "12", label: "Hẹn liên hệ lại hôm nay", change: "Cuộc hẹn tiếp theo 10:30", tone: "yellow" },
  ];
  return (
    <>
      <PageHeading page="overview" action={<Button onClick={() => onNavigate("reports")}><BarChart3 size={18} />Xem báo cáo</Button>} />
      <section className="stats-grid">{stats.map((stat) => <StatCard key={stat.label} {...stat} />)}</section>
      <section className="card orders-card">
        <div className="card-header"><div><h2>Đơn hàng gần đây</h2><p>Cập nhật theo thời gian thực</p></div><button className="text-link" onClick={() => onNavigate("invoices")}>Xem tất cả <ChevronRight size={16} /></button></div>
        <OrdersTable />
      </section>
    </>
  );
}

const genericContent: Record<Exclude<PageId, "overview" | "products" | "pos" | "reports" | "feedback">, { columns: string[]; rows: string[][] }> = {
  accounts: { columns: ["Tài khoản", "Vai trò", "Email", "Đăng nhập gần nhất", "Trạng thái"], rows: [["nguyenvana", "Quản lý", "vana@sportshop.vn", "Hôm nay, 09:42", "Đang hoạt động"], ["tranthib", "Nhân viên", "thib@sportshop.vn", "Hôm nay, 08:05", "Đang hoạt động"], ["khachhang01", "Khách hàng", "mai.nguyen@gmail.com", "Hôm qua, 21:18", "Đã khóa"]] },
  customers: { columns: ["Khách hàng", "Số điện thoại", "Hạng", "Tổng chi tiêu", "Trạng thái"], rows: [["Nguyễn Minh Anh", "0901 234 567", "Vàng", "24.580.000₫", "Đang hoạt động"], ["Trần Quốc Huy", "0987 654 321", "Bạc", "12.200.000₫", "Đang hoạt động"], ["Lê Ngọc Mai", "0912 667 889", "Thành viên", "6.840.000₫", "Đang hoạt động"]] },
  employees: { columns: ["Nhân viên", "Mã NV", "Chức vụ", "Ca làm", "Trạng thái"], rows: [["Hoàng Thị Lan", "NV012", "Tư vấn bán hàng", "08:00 – 16:00", "Đang làm việc"], ["Đỗ Văn Nam", "NV018", "Thu ngân", "08:00 – 16:00", "Đang làm việc"], ["Vũ Khánh Ly", "NV024", "CSKH", "13:00 – 21:00", "Nghỉ phép"]] },
  categories: { columns: ["Mã loại", "Tên loại sản phẩm", "Số sản phẩm", "Ngày cập nhật", "Trạng thái"], rows: [["LSP001", "Giày thể thao", "86 sản phẩm", "23/05/2025", "Đang hiển thị"], ["LSP002", "Áo thể thao", "64 sản phẩm", "22/05/2025", "Đang hiển thị"], ["LSP003", "Phụ kiện", "42 sản phẩm", "20/05/2025", "Đang hiển thị"]] },
  invoices: { columns: ["Mã HĐ", "Khách hàng", "Ngày lập", "Tổng tiền", "Trạng thái"], rows: recentOrders.slice(0, 4).map((o) => [o.id, o.name, o.date, o.total, o.status]) },
  promotions: { columns: ["Chương trình", "Loại", "Thời gian áp dụng", "Mức giảm", "Trạng thái"], rows: [["Chào hè năng động", "Theo sản phẩm", "20/05 – 10/06/2025", "Giảm đến 30%", "Đang diễn ra"], ["Freeship cuối tuần", "Theo hóa đơn", "24/05 – 26/05/2025", "Tối đa 40.000₫", "Sắp diễn ra"], ["SPORTNEW", "Voucher", "01/05 – 31/05/2025", "Giảm 15%", "Đang diễn ra"]] },
  surveys: { columns: ["Tên khảo sát", "Câu hỏi", "Lượt phản hồi", "Ngày kết thúc", "Trạng thái"], rows: [["Trải nghiệm mua sắm tháng 5", "8 câu hỏi", "428 phản hồi", "31/05/2025", "Đang thu thập"], ["Sản phẩm chạy bộ yêu thích", "6 câu hỏi", "186 phản hồi", "25/05/2025", "Đang thu thập"], ["Chất lượng phục vụ Q1", "10 câu hỏi", "752 phản hồi", "31/03/2025", "Đã kết thúc"]] },
  imports: { columns: ["Mã phiếu", "Nhà cung cấp", "Ngày nhập", "Số lượng", "Tổng giá trị"], rows: [["PN0082", "Công ty Nike Việt Nam", "23/05/2025", "120 sản phẩm", "186.400.000₫"], ["PN0081", "Adidas Distribution", "21/05/2025", "84 sản phẩm", "128.600.000₫"], ["PN0080", "Yonex Sunrise", "19/05/2025", "46 sản phẩm", "92.200.000₫"]] },
};

function FilterBar({ page, onAdd }: { page: PageId; onAdd: () => void }) {
  return (
    <div className="toolbar">
      <SearchInput placeholder={`Tìm kiếm trong ${pageMeta[page].title.toLowerCase()}...`} />
      <button className="filter-btn"><Settings2 size={17} />Bộ lọc<ChevronDown size={15} /></button>
      <div className="toolbar-spacer" />
      <Button onClick={onAdd}><Plus size={18} />Thêm mới</Button>
    </div>
  );
}

function Pagination() {
  return (
    <div className="pagination"><span>Hiển thị 1–3 trong 24 kết quả</span><div><button><ChevronLeft size={16} /></button><button className="selected">1</button><button>2</button><button>3</button><button><ChevronRight size={16} /></button></div></div>
  );
}

function GenericPage({ page, onAdd }: { page: keyof typeof genericContent; onAdd: () => void }) {
  const data = genericContent[page];
  return (
    <>
      <PageHeading page={page} />
      <div className="card data-card">
        <FilterBar page={page} onAdd={onAdd} />
        <div className="table-wrap">
          <table><thead><tr>{data.columns.map((c) => <th key={c}>{c}</th>)}<th>Thao tác</th></tr></thead>
            <tbody>{data.rows.map((row, i) => <tr key={row[0]}>{row.map((cell, j) => <td key={cell}>{j === 0 ? <strong>{cell}</strong> : j === row.length - 1 ? <StatusBadge tone={cell.includes("khóa") || cell.includes("Nghỉ") || cell.includes("kết thúc") ? "neutral" : "success"}>{cell}</StatusBadge> : <span className="muted">{cell}</span>}</td>)}<td><div className="row-actions"><IconButton icon={Eye} label="Xem" /><IconButton icon={Edit3} label="Sửa" /><IconButton icon={Trash2} label="Xóa" /></div></td></tr>)}</tbody>
          </table>
        </div>
        <Pagination />
      </div>
    </>
  );
}

function ProductsPage({ onAdd }: { onAdd: () => void }) {
  const [view, setView] = useState<"grid" | "list">("grid");
  return (
    <>
      <PageHeading page="products" />
      <div className="card data-card">
        <div className="toolbar">
          <SearchInput placeholder="Tìm tên, mã hoặc loại sản phẩm..." />
          <button className="filter-btn"><Settings2 size={17} />Loại sản phẩm<ChevronDown size={15} /></button>
          <div className="toolbar-spacer" />
          <div className="view-toggle"><button className={view === "grid" ? "active" : ""} onClick={() => setView("grid")}><Grid2X2 size={17} /></button><button className={view === "list" ? "active" : ""} onClick={() => setView("list")}><List size={18} /></button></div>
          <Button onClick={onAdd}><Plus size={18} />Thêm sản phẩm</Button>
        </div>
        {view === "grid" ? (
          <div className="product-grid">{products.map((p) => <div className="product-card" key={p.name}><div className="product-image" style={{ backgroundColor: p.color }}><span>{p.emoji}</span><button><MoreHorizontal size={18} /></button></div><div className="product-info"><div><span>{p.category}</span><StatusBadge tone="success">Đang bán</StatusBadge></div><h3>{p.name}</h3><strong>{p.price}</strong><p>Còn {p.stock} sản phẩm · Size 38–44</p></div></div>)}</div>
        ) : (
          <div className="table-wrap"><table><thead><tr><th>Sản phẩm</th><th>Loại</th><th>Giá bán</th><th>Biến thể</th><th>Tồn kho</th><th>Trạng thái</th></tr></thead><tbody>{products.map((p) => <tr key={p.name}><td><strong>{p.name}</strong></td><td>{p.category}</td><td><strong>{p.price}</strong></td><td>Size / Màu</td><td>{p.stock}</td><td><StatusBadge tone="success">Đang bán</StatusBadge></td></tr>)}</tbody></table></div>
        )}
        <Pagination />
      </div>
    </>
  );
}

function PosPage() {
  const [cart, setCart] = useState([products[0], products[1]]);
  const total = cart.length === 2 ? "4.280.000₫" : cart.length === 1 ? cart[0].price : "0₫";
  return (
    <>
      <PageHeading page="pos" action={<div className="pos-code"><Store size={17} />Quầy 01 · Ca sáng</div>} />
      <div className="pos-layout">
        <section className="card pos-products">
          <div className="toolbar"><SearchInput placeholder="Quét mã hoặc tìm sản phẩm..." /><button className="filter-btn">Tất cả loại<ChevronDown size={15} /></button></div>
          <div className="category-pills"><button className="active">Tất cả</button><button>Giày</button><button>Quần áo</button><button>Dụng cụ</button><button>Phụ kiện</button></div>
          <div className="pos-grid">{products.map((p) => <button className="pos-product" key={p.name} onClick={() => !cart.includes(p) && setCart([...cart, p])}><div style={{ backgroundColor: p.color }}>{p.emoji}</div><strong>{p.name}</strong><span>{p.price}</span><small>Còn {p.stock}</small></button>)}</div>
        </section>
        <aside className="card cart">
          <div className="cart-heading"><div><h2>Hóa đơn tạm</h2><p>{cart.length} sản phẩm</p></div><button onClick={() => setCart([])}><Trash2 size={17} /></button></div>
          <button className="customer-select"><div><UserRound size={18} /></div><span><small>Khách hàng</small><strong>Khách lẻ</strong></span><ChevronRight size={17} /></button>
          <div className="cart-items">{cart.map((p, i) => <div className="cart-item" key={p.name}><div className="cart-thumb" style={{ backgroundColor: p.color }}>{p.emoji}</div><div><strong>{p.name}</strong><span>Size {i ? "L" : "42"} · Mặc định</span><div className="qty"><button>−</button><b>1</b><button>+</button></div></div><div><strong>{p.price}</strong><button onClick={() => setCart(cart.filter((x) => x !== p))}><X size={15} /></button></div></div>)}</div>
          <div className="voucher"><BadgePercent size={18} /><input placeholder="Nhập mã giảm giá" /><button>Áp dụng</button></div>
          <div className="totals"><p><span>Tạm tính</span><strong>{total}</strong></p><p><span>Giảm giá</span><strong className="discount">−0₫</strong></p><p className="grand-total"><span>Tổng thanh toán</span><strong>{total}</strong></p></div>
          <div className="payment-methods"><button className="active"><CircleDollarSign size={18} />Tiền mặt</button><button><CreditCard size={18} />Chuyển khoản</button></div>
          <Button variant="orange" className="checkout"><Check size={20} />Thanh toán {total}</Button>
        </aside>
      </div>
    </>
  );
}

function ReportsPage() {
  const bars = [48, 62, 45, 78, 68, 88, 72];
  return (
    <>
      <PageHeading page="reports" action={<Button variant="secondary"><Download size={17} />Xuất báo cáo</Button>} />
      <div className="report-filters card"><div><CalendarClock size={18} /><span>01/05/2025 – 24/05/2025</span></div><button>Tháng này<ChevronDown size={15} /></button><Button>Xem báo cáo</Button></div>
      <div className="report-stats"><div className="card"><span>Doanh thu thuần</span><strong>1,24 tỷ ₫</strong><small>+18,4% so với kỳ trước</small></div><div className="card"><span>Đơn hàng</span><strong>3.284</strong><small>+12,8% so với kỳ trước</small></div><div className="card"><span>Giá trị trung bình</span><strong>378.000₫</strong><small>+4,6% so với kỳ trước</small></div></div>
      <div className="charts-grid">
        <section className="card revenue-chart"><div className="card-header"><div><h2>Doanh thu theo tuần</h2><p>Đơn vị: triệu đồng</p></div><StatusBadge tone="success">+18,4%</StatusBadge></div><div className="chart-area"><div className="y-axis"><span>60</span><span>40</span><span>20</span><span>0</span></div><svg viewBox="0 0 700 230" preserveAspectRatio="none"><defs><linearGradient id="area" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#0f766e" stopOpacity=".22" /><stop offset="100%" stopColor="#0f766e" stopOpacity="0" /></linearGradient></defs><path className="area" d="M0,188 C80,176 90,128 175,145 S260,95 350,110 S440,42 525,64 S620,45 700,28 L700,230 L0,230 Z" /><path className="line" d="M0,188 C80,176 90,128 175,145 S260,95 350,110 S440,42 525,64 S620,45 700,28" /></svg><div className="x-axis"><span>Tuần 1</span><span>Tuần 2</span><span>Tuần 3</span><span>Tuần 4</span></div></div></section>
        <section className="card bar-chart"><div className="card-header"><div><h2>Sản phẩm bán chạy</h2><p>Theo số lượng đã bán</p></div><MoreHorizontal size={18} /></div><div className="bars">{bars.map((h, i) => <div key={i}><div className={i === 5 ? "highlight" : ""} style={{ height: `${h}%` }} /><span>{["Giày", "Áo", "Quần", "Bóng", "Vợt", "Túi", "Khác"][i]}</span></div>)}</div></section>
      </div>
      <div className="bottom-reports"><section className="card donut-card"><div><div className="donut"><span>3.842<small>khách hàng</small></span></div><div><h2>Nhóm khách hàng</h2><p><i className="teal-dot" />18–25 tuổi <b>42%</b></p><p><i className="orange-dot" />26–35 tuổi <b>35%</b></p><p><i className="light-dot" />Trên 35 tuổi <b>23%</b></p></div></div></section><section className="card performance"><div className="card-header"><div><h2>Hiệu suất nhân viên</h2><p>Top nhân viên tháng này</p></div></div>{["Hoàng Thị Lan", "Đỗ Văn Nam", "Vũ Khánh Ly"].map((n, i) => <div className="staff-row" key={n}><span>{i + 1}</span><div className="avatar small">{n.split(" ").slice(-2).map(x => x[0]).join("")}</div><strong>{n}</strong><div><Star size={14} fill="#f97316" />{[98, 94, 91][i]}%</div></div>)}</section></div>
    </>
  );
}

function FeedbackPage() {
  const [selected, setSelected] = useState(0);
  const messages = [
    { name: "Nguyễn Minh Anh", title: "Thời gian giao hàng hơi lâu", text: "Mình đặt giày từ thứ hai nhưng đến thứ sáu mới nhận được...", time: "10 phút trước" },
    { name: "Trần Quốc Huy", title: "Tư vấn size rất nhiệt tình", text: "Nhân viên hỗ trợ mình chọn đúng size, sản phẩm rất vừa...", time: "1 giờ trước" },
    { name: "Lê Ngọc Mai", title: "Cần hỗ trợ đổi màu", text: "Mình muốn đổi sang màu đen, shop hướng dẫn giúp mình nhé.", time: "Hôm qua" },
  ];
  return (
    <>
      <PageHeading page="feedback" />
      <div className="feedback-tabs"><button className="active">Chưa trả lời <span>5</span></button><button>Đã trả lời</button></div>
      <div className="feedback-layout card">
        <div className="message-list"><SearchInput placeholder="Tìm phản hồi..." />{messages.map((m, i) => <button key={m.name} className={selected === i ? "active" : ""} onClick={() => setSelected(i)}><div className="avatar small">{m.name.split(" ").slice(-2).map(x => x[0]).join("")}</div><div><p><strong>{m.name}</strong><span>{m.time}</span></p><b>{m.title}</b><small>{m.text}</small></div></button>)}</div>
        <div className="message-detail"><div className="message-detail-head"><div className="avatar">{messages[selected].name.split(" ").slice(-2).map(x => x[0]).join("")}</div><div><strong>{messages[selected].name}</strong><span>Khách hàng thân thiết · #KH0284</span></div><StatusBadge tone="warning">Chưa trả lời</StatusBadge></div><div className="message-content"><span>Nội dung phản hồi</span><h2>{messages[selected].title}</h2><p>Chào shop, {messages[selected].text} Mình mong shop có thể cải thiện phần vận chuyển để trải nghiệm lần sau tốt hơn. Cảm ơn shop!</p><small>Gửi lúc 09:42, 24/05/2025</small></div><div className="reply-box"><label>Phản hồi khách hàng</label><textarea placeholder="Nhập nội dung phản hồi..." /><div><span>Phản hồi sẽ được gửi qua email</span><Button><Send size={17} />Gửi phản hồi</Button></div></div></div>
      </div>
    </>
  );
}

function Drawer({ open, onClose, page }: { open: boolean; onClose: () => void; page: PageId }) {
  if (!open) return null;
  return (
    <div className="drawer-overlay" onMouseDown={onClose}><aside className="drawer" onMouseDown={(e) => e.stopPropagation()}><div className="drawer-head"><div><span>THÊM MỚI</span><h2>{pageMeta[page].title}</h2></div><button onClick={onClose}><X size={20} /></button></div><div className="drawer-body"><label>Tên / Tiêu đề<input placeholder="Nhập thông tin" /></label><label>Mã định danh<input placeholder="Tự động tạo nếu để trống" /></label><label>Phân loại<select><option>Chọn phân loại</option><option>Đang hoạt động</option></select></label><label>Ghi chú<textarea placeholder="Thông tin bổ sung..." /></label></div><div className="drawer-footer"><Button variant="secondary" onClick={onClose}>Hủy bỏ</Button><Button onClick={onClose}><Check size={17} />Lưu thông tin</Button></div></aside></div>
  );
}

function Login({ onLogin }: { onLogin: () => void }) {
  const [showPassword, setShowPassword] = useState(false);
  return (
    <div className="login-page">
      <section className="login-visual">
        <img src="https://images.unsplash.com/photo-1774803561549-3dbab86133d2?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxzdHlsaXNoJTIwYXRobGV0ZSUyMHJ1bm5pbmclMjBzcG9ydHN3ZWFyJTIwZGFyayUyMHN0dWRpbyUyMHRlYWx8ZW58MXx8fHwxNzkxNDQ3MTU3fDA&ixlib=rb-4.1.0&q=85&w=1400" alt="Vận động viên trong ánh sáng studio" />
        <div className="login-overlay" />
        <div className="login-brand"><Logo /><div><span>QUẢN TRỊ CỬA HÀNG THÔNG MINH</span><h2>Vận hành tốt hơn.<br />Bứt phá mỗi ngày.</h2><p>Một nền tảng duy nhất để theo dõi doanh thu, quản lý đội ngũ và chăm sóc khách hàng.</p></div><small>Ảnh: Prateek Saxena · Unsplash</small></div>
      </section>
      <section className="login-form-wrap">
        <form className="login-form" onSubmit={(e) => { e.preventDefault(); onLogin(); }}>
          <div className="mobile-logo"><Logo /></div>
          <span className="eyebrow">SPORTSHOP ADMIN</span><h1>Chào mừng trở lại</h1><p>Đăng nhập để tiếp tục quản lý cửa hàng của bạn.</p>
          <label>Tài khoản<div><UserRound size={18} /><input defaultValue="quanlyminh" placeholder="Nhập tài khoản" /></div></label>
          <label>Mật khẩu<div><Lock size={18} /><input type={showPassword ? "text" : "password"} defaultValue="12345678" /><button type="button" onClick={() => setShowPassword(!showPassword)}><Eye size={18} /></button></div></label>
          <div className="remember"><label><input type="checkbox" defaultChecked />Ghi nhớ đăng nhập</label><button type="button">Quên mật khẩu?</button></div>
          <Button type="submit" className="login-button">Đăng nhập <ChevronRight size={18} /></Button>
          <div className="login-help"><Mail size={17} />Cần hỗ trợ? <strong>support@sportshop.vn</strong></div>
        </form>
      </section>
    </div>
  );
}

type CustomerPage = "home" | "login" | "register" | "profile" | "feedback" | "surveys";

const customerPhoto = "https://images.unsplash.com/photo-1771402899305-2066905a74f1?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxWaWV0bmFtZXNlJTIwYXRobGV0ZSUyMHJ1bm5pbmclMjB0cmFpbmluZyUyMHVyYmFuJTIwc3BvcnRzJTIwdGVhbHxlbnwxfHx8fDE3OTIwMzcyNTd8MA&ixlib=rb-4.1.0&q=80&w=1400";

function Notice({ children, tone = "success" }: { children: ReactNode; tone?: "success" | "error" }) {
  return <div className={`customer-notice ${tone}`} role="status">{tone === "success" ? <CheckCircle2 size={17} /> : <AlertCircle size={17} />}{children}</div>;
}

function CustomerAuth({
  page,
  onNavigate,
  onAuthenticated,
}: {
  page: "login" | "register";
  onNavigate: (page: CustomerPage) => void;
  onAuthenticated: () => void;
}) {
  const isRegister = page === "register";
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const submit = (form: HTMLFormElement) => {
    const data = new FormData(form);
    const nextErrors: Record<string, string> = {};
    if (!String(data.get("username") || "").trim()) nextErrors.username = "Vui lòng nhập tên đăng nhập.";
    if (isRegister && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(data.get("email") || ""))) nextErrors.email = "Email chưa đúng định dạng.";
    if (isRegister && !/^0\d{9}$/.test(String(data.get("phone") || ""))) nextErrors.phone = "Số điện thoại cần gồm 10 chữ số.";
    if (String(data.get("password") || "").length < 6) nextErrors.password = "Mật khẩu cần có ít nhất 6 ký tự.";
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) return;
    setLoading(true);
    window.setTimeout(() => {
      setLoading(false);
      if (isRegister) {
        setMessage("Đăng ký thành công! Bạn có thể đăng nhập ngay.");
      } else {
        setMessage("Đăng nhập thành công! Đang chuyển đến cửa hàng...");
        window.setTimeout(onAuthenticated, 650);
      }
    }, 650);
  };
  return (
    <div className="customer-auth">
      <section className="customer-auth-visual">
        <img src={customerPhoto} alt="Vận động viên đang thi đấu trên đường chạy" />
        <div className="customer-auth-shade" />
        <div className="customer-auth-story">
          <button className="auth-back" onClick={() => onNavigate("home")}><ArrowLeft size={17} />Về cửa hàng</button>
          <Logo />
          <div><span>SỐNG KHỎE · CHƠI HẾT MÌNH</span><h2>{isRegister ? "Bắt đầu hành trình của riêng bạn." : "Chào mừng bạn trở lại đường đua."}</h2><p>Đăng nhập để lưu sản phẩm yêu thích, quản lý thông tin và chia sẻ trải nghiệm cùng SPORTSHOP.</p></div>
          <small>Ảnh: Ben Iwara · Unsplash</small>
        </div>
      </section>
      <section className="customer-auth-form-wrap">
        <form className="customer-auth-form" onSubmit={(e) => { e.preventDefault(); submit(e.currentTarget); }}>
          <div className="auth-mobile-head"><Logo /><button type="button" onClick={() => onNavigate("home")}><X size={19} /></button></div>
          <span className="eyebrow">TÀI KHOẢN SPORTSHOP</span>
          <h1>{isRegister ? "Đăng ký tài khoản khách hàng" : "Đăng nhập"}</h1>
          <p>{isRegister ? "Tạo tài khoản để nhận trải nghiệm mua sắm dành riêng cho bạn." : "Tiếp tục hành trình mua sắm và vận động của bạn."}</p>
          {message && <Notice>{message}</Notice>}
          <label className={errors.username ? "has-error" : ""}>Tên đăng nhập<div><UserRound size={18} /><input name="username" placeholder="Ví dụ: minhanh_98" defaultValue={isRegister ? "" : "minhanh_98"} /></div>{errors.username && <small>{errors.username}</small>}</label>
          {isRegister && <label className={errors.email ? "has-error" : ""}>Email<div><Mail size={18} /><input name="email" type="email" placeholder="ban@email.com" /></div>{errors.email && <small>{errors.email}</small>}</label>}
          {isRegister && <label className={errors.phone ? "has-error" : ""}>Số điện thoại<div><Headphones size={18} /><input name="phone" inputMode="numeric" placeholder="09xx xxx xxx" /></div>{errors.phone && <small>{errors.phone}</small>}</label>}
          <label className={errors.password ? "has-error" : ""}>Mật khẩu<div><Lock size={18} /><input name="password" type={showPassword ? "text" : "password"} placeholder="Tối thiểu 6 ký tự" defaultValue={isRegister ? "" : "12345678"} /><button type="button" onClick={() => setShowPassword(!showPassword)} aria-label="Hiện hoặc ẩn mật khẩu">{showPassword ? <EyeOff size={18} /> : <Eye size={18} />}</button></div>{errors.password && <small>{errors.password}</small>}</label>
          <Button type="submit" className="customer-submit">{loading ? "Đang xử lý..." : isRegister ? "Đăng ký" : "Đăng nhập"}{!loading && (isRegister ? <UserPlus size={17} /> : <ChevronRight size={17} />)}</Button>
          <div className="auth-switch">{isRegister ? "Đã có tài khoản?" : "Chưa có tài khoản?"}<button type="button" onClick={() => { setErrors({}); setMessage(""); onNavigate(isRegister ? "login" : "register"); }}>{isRegister ? "Đăng nhập" : "Đăng ký"}</button></div>
        </form>
      </section>
    </div>
  );
}

function CustomerShell({
  active,
  onNavigate,
  onLogout,
  children,
}: {
  active: CustomerPage;
  onNavigate: (page: CustomerPage) => void;
  onLogout: () => void;
  children: ReactNode;
}) {
  return (
    <div className="customer-area">
      <header className="customer-header">
        <button className="customer-logo-button" onClick={() => onNavigate("home")}><Logo /></button>
        <nav><button onClick={() => onNavigate("home")}>Cửa hàng</button><button className={active === "profile" ? "active" : ""} onClick={() => onNavigate("profile")}>Chỉnh sửa thông tin</button><button className={active === "feedback" ? "active" : ""} onClick={() => onNavigate("feedback")}>Gửi phản hồi về sản phẩm</button><button className={active === "surveys" ? "active" : ""} onClick={() => onNavigate("surveys")}>Khảo sát khách hàng</button></nav>
        <div className="customer-user"><div className="avatar">MA</div><span><strong>Minh Anh</strong><small>Thành viên Bạc</small></span><button onClick={onLogout} title="Đăng xuất"><LogOut size={18} /></button></div>
      </header>
      <main className="customer-content">{children}</main>
      <footer className="customer-mini-footer"><Logo /><span>© 2025 SPORTSHOP · Chinh phục giới hạn mỗi ngày.</span><span>support@sportshop.vn · 1900 6868</span></footer>
    </div>
  );
}

function CustomerPageTitle({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }) {
  return <div className="customer-page-title"><span>{eyebrow}</span><h1>{title}</h1><p>{description}</p></div>;
}

function CustomerProfile() {
  const [saved, setSaved] = useState(false);
  const [emailError, setEmailError] = useState("");
  return (
    <>
      <CustomerPageTitle eyebrow="TÀI KHOẢN CỦA TÔI" title="Chỉnh sửa thông tin" description="Thông tin chính xác giúp SPORTSHOP phục vụ bạn tốt hơn." />
      <div className="customer-profile-layout">
        <aside className="profile-summary card"><div className="profile-avatar">MA</div><h2>Nguyễn Minh Anh</h2><p>Khách hàng từ tháng 08/2023</p><div><span><ShoppingBag size={18} /><b>12</b><small>Đơn hàng</small></span><span><Star size={18} /><b>1.240</b><small>Điểm thưởng</small></span></div><small>Hồ sơ đã hoàn thiện 85%</small><i><b /></i></aside>
        <form className="customer-form card" onSubmit={(e) => { e.preventDefault(); const email = String(new FormData(e.currentTarget).get("email") || ""); if (!email.includes("@")) { setEmailError("Vui lòng nhập email hợp lệ."); setSaved(false); } else { setEmailError(""); setSaved(true); } }}>
          <div className="form-card-head"><div><h2>Thông tin cá nhân</h2><p>Cập nhật lần cuối: 20/05/2025</p></div><ShieldCheck size={22} /></div>
          {saved && <Notice>Thông tin của bạn đã được cập nhật thành công.</Notice>}
          <div className="form-grid">
            <label>Họ và tên<input name="fullName" defaultValue="Nguyễn Minh Anh" /></label>
            <label>Tên đăng nhập<input defaultValue="minhanh_98" readOnly /><small>Tên đăng nhập không thể thay đổi</small></label>
            <label className={emailError ? "has-error" : ""}>Email<input name="email" defaultValue="minhanh98@gmail.com" />{emailError && <small>{emailError}</small>}</label>
            <label>Số điện thoại<input defaultValue="0912 345 678" /></label>
            <label>Ngày sinh<input type="date" defaultValue="1998-08-20" /></label>
            <label>Giới tính<select defaultValue="Nữ"><option>Nữ</option><option>Nam</option><option>Khác</option></select></label>
            <label className="full-field">Sở thích thể thao<div className="interest-options">{["Chạy bộ", "Cầu lông", "Gym", "Bóng đá", "Yoga"].map((item, index) => <button type="button" className={index < 3 ? "selected" : ""} key={item}>{index < 3 && <Check size={13} />}{item}</button>)}</div></label>
          </div>
          <div className="customer-form-actions"><Button variant="secondary" type="button">Hủy</Button><Button type="submit"><Check size={17} />Lưu thay đổi</Button></div>
        </form>
      </div>
    </>
  );
}

function CustomerFeedback() {
  const [rating, setRating] = useState(0);
  const [sent, setSent] = useState(false);
  const [detail, setDetail] = useState(false);
  const feedbacks = [
    { id: "PH0238", product: "Nike Air Zoom Pegasus 41", text: "Giày nhẹ, đệm êm và rất phù hợp để chạy dài.", date: "22/05/2025", status: "Đã trả lời", tone: "success" as StatusTone },
    { id: "PH0214", product: "Áo Adidas Own The Run", text: "Chất liệu thoáng, nhưng mình cần hỗ trợ đổi size.", date: "18/05/2025", status: "Đang xử lý", tone: "warning" as StatusTone },
    { id: "PH0189", product: "Túi Nike Brasilia 9.5", text: "Ngăn đựng rộng và đường may chắc chắn.", date: "03/05/2025", status: "Mới", tone: "info" as StatusTone },
  ];
  return (
    <>
      <CustomerPageTitle eyebrow="LẮNG NGHE & CẢI THIỆN" title="Gửi phản hồi về sản phẩm" description="Chia sẻ trải nghiệm để chúng tôi nâng cao chất lượng sản phẩm và dịch vụ." />
      <section className="feedback-compose card">
        <div className="feedback-intro"><div><MessageSquareText size={25} /></div><h2>Trải nghiệm của bạn thế nào?</h2><p>Mỗi phản hồi đều được đội ngũ SPORTSHOP đọc và xử lý.</p></div>
        <form onSubmit={(e) => { e.preventDefault(); setSent(true); }}>
          {sent && <Notice>Phản hồi đã được gửi. Cảm ơn bạn đã chia sẻ!</Notice>}
          <label>Chọn sản phẩm<select required defaultValue=""><option value="" disabled>Chọn sản phẩm đã mua</option>{shopProducts.map((p) => <option key={p.id}>{p.name}</option>)}</select></label>
          <label>Đánh giá sản phẩm<div className="rating-input">{[1, 2, 3, 4, 5].map((star) => <button type="button" key={star} onClick={() => setRating(star)} aria-label={`${star} sao`}><Star size={25} fill={star <= rating ? "currentColor" : "none"} /></button>)}<span>{rating ? `${rating}/5 · ${rating >= 4 ? "Rất hài lòng" : "Cảm ơn đánh giá của bạn"}` : "Chọn số sao"}</span></div></label>
          <label>Nội dung phản hồi<textarea required placeholder="Hãy chia sẻ điều bạn thích hoặc góp ý để sản phẩm tốt hơn..." /></label>
          <div className="customer-form-actions"><Button variant="secondary" type="button">Hủy</Button><Button type="submit"><Send size={17} />Gửi phản hồi</Button></div>
        </form>
      </section>
      <section className="feedback-history card">
        <div className="form-card-head"><div><h2>Phản hồi đã gửi</h2><p>Theo dõi trạng thái xử lý phản hồi của bạn</p></div><span>03 phản hồi</span></div>
        <div className="table-wrap"><table><thead><tr><th>Mã phản hồi</th><th>Sản phẩm</th><th>Nội dung phản hồi</th><th>Ngày gửi</th><th>Trạng thái</th><th /></tr></thead><tbody>{feedbacks.map((item) => <tr key={item.id}><td><strong className="order-code">{item.id}</strong></td><td><strong>{item.product}</strong></td><td className="feedback-text">{item.text}</td><td className="muted">{item.date}</td><td><StatusBadge tone={item.tone}>{item.status}</StatusBadge></td><td><button className="detail-link" onClick={() => setDetail(true)}>Xem chi tiết</button></td></tr>)}</tbody></table></div>
      </section>
      {detail && <div className="customer-modal-backdrop" onMouseDown={() => setDetail(false)}><div className="customer-modal" onMouseDown={(e) => e.stopPropagation()}><div className="modal-head"><div><span>PHẢN HỒI PH0238</span><h2>Chi tiết phản hồi</h2></div><button onClick={() => setDetail(false)}><X size={19} /></button></div><div className="modal-body"><StatusBadge tone="success">Đã trả lời</StatusBadge><h3>Nike Air Zoom Pegasus 41</h3><small>Phản hồi của bạn · 22/05/2025</small><p>Giày nhẹ, đệm êm và rất phù hợp để chạy dài. Mình đã dùng cho buổi chạy 10km và cảm thấy rất thoải mái.</p><div className="store-reply"><strong><CheckCircle2 size={16} />Phản hồi từ SPORTSHOP</strong><p>Cảm ơn Minh Anh đã tin chọn sản phẩm. Chúc bạn có thêm nhiều buổi chạy thật bứt phá!</p></div></div></div></div>}
    </>
  );
}

function CustomerSurveys() {
  const [survey, setSurvey] = useState<"list" | "doing" | "done">("list");
  const [step, setStep] = useState(0);
  const [answered, setAnswered] = useState(false);
  const questions = [
    { title: "Bạn thường mua sắm đồ thể thao với tần suất nào?", type: "one", options: ["Hàng tháng", "Mỗi 2–3 tháng", "Mỗi 6 tháng", "Khi có nhu cầu"] },
    { title: "Bạn quan tâm đến những nhóm sản phẩm nào?", type: "many", options: ["Giày chạy bộ", "Quần áo tập luyện", "Dụng cụ thể thao", "Phụ kiện"] },
    { title: "Mức độ hài lòng của bạn với trải nghiệm mua sắm?", type: "scale", options: ["1", "2", "3", "4", "5"] },
    { title: "Điều gì SPORTSHOP có thể làm tốt hơn?", type: "text", options: [] },
  ];
  if (survey === "done") return <div className="survey-thanks card"><div><CheckCircle2 size={34} /></div><span>HOÀN TẤT KHẢO SÁT</span><h1>Cảm ơn bạn đã hoàn thành khảo sát!</h1><p>Ý kiến của bạn là dữ liệu quý giá để SPORTSHOP tạo nên trải nghiệm tốt hơn mỗi ngày.</p><Button onClick={() => { setSurvey("list"); setStep(0); setAnswered(false); }}><ArrowLeft size={17} />Quay lại danh sách khảo sát</Button></div>;
  if (survey === "doing") {
    const question = questions[step];
    return (
      <div className="survey-doing">
        <button className="back-link" onClick={() => setSurvey("list")}><ArrowLeft size={17} />Danh sách khảo sát</button>
        <div className="survey-form card">
          <div className="survey-progress-head"><div><span>KHẢO SÁT TRẢI NGHIỆM MUA SẮM 2025</span><h1>Hiểu bạn hơn, phục vụ tốt hơn</h1></div><b>{step + 1}/{questions.length}</b></div>
          <div className="progress-track"><i style={{ width: `${((step + 1) / questions.length) * 100}%` }} /></div>
          <div className="survey-question"><span>CÂU HỎI {String(step + 1).padStart(2, "0")} · BẮT BUỘC</span><h2>{question.title}</h2><p>Chọn phương án phù hợp nhất với bạn.</p>
            {question.type === "text" ? <textarea onChange={(e) => setAnswered(Boolean(e.target.value.trim()))} placeholder="Nhập câu trả lời của bạn..." /> : <div className={`survey-options ${question.type === "scale" ? "scale" : ""}`}>{question.options.map((option) => <label key={option}><input type={question.type === "many" ? "checkbox" : "radio"} name="survey-answer" onChange={() => setAnswered(true)} /><span>{option}</span></label>)}</div>}
            {!answered && <small className="survey-hint">Vui lòng trả lời câu hỏi trước khi tiếp tục.</small>}
          </div>
          <div className="survey-actions"><Button variant="secondary" onClick={() => { setStep(Math.max(0, step - 1)); setAnswered(step > 0); }}><ChevronLeft size={17} />Câu trước</Button><span>Câu hỏi bắt buộc được đánh dấu *</span><Button onClick={() => { if (!answered) return; if (step === questions.length - 1) setSurvey("done"); else { setStep(step + 1); setAnswered(false); } }}>{step === questions.length - 1 ? "Gửi khảo sát" : "Câu tiếp theo"}<ChevronRight size={17} /></Button></div>
        </div>
      </div>
    );
  }
  return (
    <>
      <CustomerPageTitle eyebrow="TIẾNG NÓI KHÁCH HÀNG" title="Khảo sát khách hàng" description="Các khảo sát được SPORTSHOP gửi riêng đến tài khoản của bạn." />
      <div className="survey-summary"><div><ClipboardCheck size={21} /><span><strong>01 khảo sát đang chờ</strong><small>Mất khoảng 3–5 phút để hoàn thành</small></span></div><span>Đã hoàn thành <b>3</b> khảo sát</span></div>
      <div className="survey-list">
        <article className="survey-card featured"><div className="survey-card-icon"><ClipboardList size={25} /></div><div className="survey-card-copy"><div><span>ĐANG DIỄN RA</span><small><CalendarClock size={14} />Hạn: 31/05/2025</small></div><h2>Khảo sát trải nghiệm mua sắm 2025</h2><p>Chia sẻ cảm nhận về sản phẩm, dịch vụ và trải nghiệm của bạn tại SPORTSHOP.</p><div className="survey-meta"><span>4 câu hỏi</span><span>Khoảng 3 phút</span><span>+100 điểm thành viên</span></div></div><Button onClick={() => setSurvey("doing")}>Thực hiện khảo sát<ChevronRight size={17} /></Button></article>
        <article className="survey-card completed"><div className="survey-card-icon"><CheckCircle2 size={25} /></div><div className="survey-card-copy"><div><span>ĐÃ HOÀN THÀNH</span><small>15/04/2025</small></div><h2>Thói quen vận động của bạn</h2><p>Cảm ơn bạn đã chia sẻ thói quen và mục tiêu luyện tập.</p></div><StatusBadge tone="success">Đã gửi</StatusBadge></article>
      </div>
    </>
  );
}

const shopProducts = [
  {
    id: 1,
    name: "Nike Air Zoom Pegasus 41",
    type: "Giày chạy bộ",
    price: "3.290.000₫",
    oldPrice: "3.690.000₫",
    badge: "-11%",
    image: "https://images.unsplash.com/photo-1739138053507-0f312a938451?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxydW5uaW5nJTIwc2hvZXMlMjBzcG9ydHMlMjBzdG9yZXxlbnwxfHx8fDE3OTE0NzEyMTl8MA&ixlib=rb-4.1.0&q=85&w=900",
  },
  {
    id: 2,
    name: "Adidas Ultraboost Light",
    type: "Giày luyện tập",
    price: "4.190.000₫",
    oldPrice: "",
    badge: "Mới",
    image: "https://images.unsplash.com/photo-1772909849803-e1c298df3fd9?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwyfHxydW5uaW5nJTIwc2hvZXMlMjBzcG9ydHMlMjBzdG9yZXxlbnwxfHx8fDE3OTE0NzEyMTl8MA&ixlib=rb-4.1.0&q=85&w=900",
  },
  {
    id: 3,
    name: "Puma Velocity Nitro 3",
    type: "Giày chạy bộ",
    price: "2.690.000₫",
    oldPrice: "2.990.000₫",
    badge: "-10%",
    image: "https://images.unsplash.com/photo-1760302318631-a8d342cd4951?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwzfHxydW5uaW5nJTIwc2hvZXMlMjBzcG9ydHMlMjBzdG9yZXxlbnwxfHx8fDE3OTE0NzEyMTl8MA&ixlib=rb-4.1.0&q=85&w=900",
  },
  {
    id: 4,
    name: "Nike Air Max Pulse",
    type: "Giày thời trang",
    price: "3.850.000₫",
    oldPrice: "",
    badge: "Bán chạy",
    image: "https://images.unsplash.com/photo-1739138053507-0f312a938451?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxydW5uaW5nJTIwc2hvZXMlMjBzcG9ydHMlMjBzdG9yZXxlbnwxfHx8fDE3OTE0NzEyMTl8MA&ixlib=rb-4.1.0&q=85&w=900",
  },
];

function StoreProductCard({ product, onAdd }: { product: (typeof shopProducts)[number]; onAdd: () => void }) {
  const [liked, setLiked] = useState(false);
  return (
    <article className="shop-product-card">
      <div className="shop-product-image">
        <img src={product.image} alt={product.name} />
        <span className={product.badge === "Mới" ? "new" : ""}>{product.badge}</span>
        <button className={liked ? "liked" : ""} onClick={() => setLiked(!liked)} aria-label="Yêu thích"><Heart size={18} fill={liked ? "currentColor" : "none"} /></button>
        <button className="quick-add" onClick={onAdd}><ShoppingBag size={16} />Thêm vào giỏ</button>
      </div>
      <div className="shop-product-info">
        <div><span>{product.type}</span><span><Star size={12} fill="#f97316" />4.9</span></div>
        <h3>{product.name}</h3>
        <p><strong>{product.price}</strong>{product.oldPrice && <del>{product.oldPrice}</del>}</p>
        <div className="color-swatches"><i /><i /><i /></div>
      </div>
    </article>
  );
}

function Storefront({ onAdmin, loggedIn, onNavigate, onLogout }: { onAdmin: () => void; loggedIn: boolean; onNavigate: (page: CustomerPage) => void; onLogout: () => void }) {
  const [cartCount, setCartCount] = useState(2);
  const [activeCategory, setActiveCategory] = useState("Tất cả");
  const [searchOpen, setSearchOpen] = useState(false);
  const categories = [
    { name: "Giày chạy bộ", sub: "86 sản phẩm", className: "running", icon: Zap },
    { name: "Đồ tập nam", sub: "124 sản phẩm", className: "men", icon: UserRound },
    { name: "Đồ tập nữ", sub: "108 sản phẩm", className: "women", icon: Sparkles },
    { name: "Dụng cụ", sub: "72 sản phẩm", className: "gear", icon: Activity },
  ];
  return (
    <div className="storefront">
      <div className="shop-promo"><span>FREESHIP toàn quốc cho đơn từ 500.000₫</span><button>Theo dõi đơn hàng</button><button onClick={onAdmin}>Trang quản trị</button></div>
      <header className="shop-header">
        <Logo />
        <nav>
          <button className="active">Trang chủ</button>
          <button>Sản phẩm</button>
          <button className="sale">Khuyến mãi</button>
        </nav>
        <div className="shop-actions">
          <button onClick={() => setSearchOpen(!searchOpen)}><Search size={20} /></button>
          <button><Heart size={20} /></button>
          <button className="cart-button"><ShoppingBag size={20} /><span>{cartCount}</span></button>
          {loggedIn ? <div className="shop-account"><button onClick={() => onNavigate("profile")}><div className="avatar small">MA</div><span><strong>Minh Anh</strong><small>Tài khoản</small></span><ChevronDown size={15} /></button><div><button onClick={() => onNavigate("profile")}>Chỉnh sửa thông tin</button><button onClick={() => onNavigate("feedback")}>Gửi phản hồi về sản phẩm</button><button onClick={() => onNavigate("surveys")}>Khảo sát khách hàng</button><button onClick={onLogout}>Đăng xuất</button></div></div> : <div className="shop-auth-actions"><button onClick={() => onNavigate("login")}>Đăng nhập</button><Button onClick={() => onNavigate("register")}>Đăng ký</Button></div>}
        </div>
      </header>
      {searchOpen && <div className="shop-search"><Search size={20} /><input autoFocus placeholder="Bạn đang tìm sản phẩm nào?" /><kbd>ESC</kbd></div>}

      <main className="shop-main">
        <section className="shop-hero">
          <div className="hero-copy">
            <span className="hero-label"><Sparkles size={15} />BỘ SƯU TẬP MỚI 2025</span>
            <h1>Chuyển động<br /><em>theo cách của bạn.</em></h1>
            <p>Trang bị tốt hơn cho từng bước chạy. Khám phá những thiết kế mới nhất, tạo ra để bạn vượt qua mọi giới hạn.</p>
            <div><Button className="hero-button">Mua sắm ngay <ChevronRight size={18} /></Button><button className="hero-link">Khám phá bộ sưu tập <span>↗</span></button></div>
            <div className="hero-stats"><div><strong>10K+</strong><span>Khách hàng tin chọn</span></div><div><strong>500+</strong><span>Sản phẩm chính hãng</span></div><div><strong>4.9/5</strong><span>Đánh giá trung bình</span></div></div>
          </div>
          <div className="hero-image">
            <img src="https://images.unsplash.com/photo-1739138053507-0f312a938451?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxydW5uaW5nJTIwc2hvZXMlMjBzcG9ydHMlMjBzdG9yZXxlbnwxfHx8fDE3OTE0NzEyMTl8MA&ixlib=rb-4.1.0&q=90&w=1400" alt="Giày thể thao mới tại SPORTSHOP" />
            <div className="hero-price"><span>Sản phẩm nổi bật</span><strong>Nike Pegasus 41</strong><small>từ 3.290.000₫</small></div>
            <div className="hero-scroll"><span>SCROLL</span><i /></div>
          </div>
        </section>

        <section className="shop-benefits">
          <div><Truck size={22} /><span><strong>Giao hàng miễn phí</strong><small>Đơn hàng từ 500.000₫</small></span></div>
          <div><ShieldCheck size={22} /><span><strong>100% chính hãng</strong><small>Cam kết hoàn tiền</small></span></div>
          <div><Activity size={22} /><span><strong>Đổi trả trong 30 ngày</strong><small>Nhanh chóng, dễ dàng</small></span></div>
          <div><Headphones size={22} /><span><strong>Hỗ trợ tận tâm</strong><small>Từ 8:00 đến 22:00</small></span></div>
        </section>

        <section className="shop-section">
          <div className="shop-section-heading"><div><span>CHỌN THEO NHU CẦU</span><h2>Tìm đúng đồ, chơi đúng chất</h2></div><button>Xem tất cả danh mục <ChevronRight size={17} /></button></div>
          <div className="shop-category-grid">
            {categories.map(({ name, sub, className, icon: Icon }) => <button key={name} className={`shop-category ${className}`}><Icon size={30} /><span><strong>{name}</strong><small>{sub}</small></span><i><ChevronRight size={18} /></i></button>)}
          </div>
        </section>

        <section className="shop-section products-section">
          <div className="shop-section-heading">
            <div><span>ĐƯỢC YÊU THÍCH NHẤT</span><h2>Sản phẩm nổi bật</h2></div>
            <div className="product-tabs">{["Tất cả", "Giày", "Quần áo", "Phụ kiện"].map((tab) => <button key={tab} className={activeCategory === tab ? "active" : ""} onClick={() => setActiveCategory(tab)}>{tab}</button>)}</div>
          </div>
          <div className="shop-products">{shopProducts.map((product) => <StoreProductCard key={product.id} product={product} onAdd={() => setCartCount(cartCount + 1)} />)}</div>
          <Button variant="secondary" className="view-products">Xem tất cả sản phẩm <ChevronRight size={17} /></Button>
        </section>

        <section className="shop-campaign">
          <div><span>CHỈ TRONG TUẦN NÀY</span><h2>Chạy xa hơn.<br />Tiết kiệm nhiều hơn.</h2><p>Giảm đến <strong>40%</strong> các sản phẩm chạy bộ chọn lọc.</p><Button variant="orange">Mua ngay <ChevronRight size={18} /></Button></div>
          <div className="campaign-card"><span>EXTRA</span><strong>15%</strong><small>Nhập mã</small><b>RUN15</b></div>
          <div className="campaign-shoe"><img src={shopProducts[1].image} alt="Bộ sưu tập giày chạy bộ khuyến mãi" /></div>
        </section>

        <section className="shop-newsletter">
          <div><Mail size={24} /><span><strong>Đăng ký nhận ưu đãi</strong><small>Nhận ngay voucher 100.000₫ cho đơn hàng đầu tiên.</small></span></div>
          <label><input placeholder="Email của bạn" /><Button>Đăng ký</Button></label>
        </section>
      </main>

      <footer className="shop-footer">
        <div><Logo /><p>Đồng hành cùng bạn trên hành trình sống khỏe và chinh phục mọi giới hạn.</p><span><MapPin size={15} />128 Nguyễn Trãi, Quận 1, TP.HCM</span></div>
        <div><strong>Mua sắm</strong><button>Giày thể thao</button><button>Quần áo</button><button>Dụng cụ</button><button>Khuyến mãi</button></div>
        <div><strong>Hỗ trợ</strong><button>Chính sách đổi trả</button><button>Giao hàng</button><button>Hướng dẫn chọn size</button><button>Liên hệ</button></div>
        <div><strong>SPORTSHOP</strong><button>Về chúng tôi</button><button>Hệ thống cửa hàng</button><button>Tuyển dụng</button><button onClick={onAdmin}>Đăng nhập quản trị</button></div>
      </footer>
      <nav className="shop-mobile-nav"><button className="active"><Store size={20} /><span>Trang chủ</span></button><button><Search size={20} /><span>Tìm kiếm</span></button><button><Heart size={20} /><span>Yêu thích</span></button><button className="cart-button"><ShoppingBag size={20} /><span>Giỏ hàng</span><i>{cartCount}</i></button><button onClick={() => onNavigate(loggedIn ? "profile" : "login")}><UserRound size={20} /><span>Tài khoản</span></button></nav>
    </div>
  );
}

export default function App() {
  const [mode, setMode] = useState<"shop" | "admin">("shop");
  const [loggedIn, setLoggedIn] = useState(true);
  const [customerLoggedIn, setCustomerLoggedIn] = useState(false);
  const [customerPage, setCustomerPage] = useState<CustomerPage>("home");
  const [page, setPage] = useState<PageId>("overview");
  const [drawer, setDrawer] = useState(false);
  const [collapsed, setCollapsed] = useState(false);
  const content = useMemo(() => {
    if (page === "overview") return <Overview onNavigate={setPage} />;
    if (page === "products") return <ProductsPage onAdd={() => setDrawer(true)} />;
    if (page === "pos") return <PosPage />;
    if (page === "reports") return <ReportsPage />;
    if (page === "feedback") return <FeedbackPage />;
    return <GenericPage page={page as keyof typeof genericContent} onAdd={() => setDrawer(true)} />;
  }, [page]);
  if (mode === "shop") {
    if (customerPage === "login" || customerPage === "register") return <CustomerAuth page={customerPage} onNavigate={setCustomerPage} onAuthenticated={() => { setCustomerLoggedIn(true); setCustomerPage("home"); }} />;
    if (customerPage !== "home" && customerLoggedIn) return <CustomerShell active={customerPage} onNavigate={setCustomerPage} onLogout={() => { setCustomerLoggedIn(false); setCustomerPage("home"); }}>{customerPage === "profile" ? <CustomerProfile /> : customerPage === "feedback" ? <CustomerFeedback /> : <CustomerSurveys />}</CustomerShell>;
    return <Storefront onAdmin={() => setMode("admin")} loggedIn={customerLoggedIn} onNavigate={setCustomerPage} onLogout={() => setCustomerLoggedIn(false)} />;
  }
  if (!loggedIn) return <Login onLogin={() => setLoggedIn(true)} />;
  return (
    <div className={`app-shell ${collapsed ? "is-collapsed" : ""}`}>
      <Sidebar active={page} onChange={setPage} collapsed={collapsed} onToggle={() => setCollapsed(!collapsed)} />
      <div className="main-shell">
        <Header onLogout={() => setLoggedIn(false)} onMenu={() => setCollapsed(!collapsed)} onStore={() => setMode("shop")} />
        <main>{content}</main>
      </div>
      <Drawer open={drawer} onClose={() => setDrawer(false)} page={page} />
    </div>
  );
}
