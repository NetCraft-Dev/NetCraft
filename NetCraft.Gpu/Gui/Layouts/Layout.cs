namespace NetCraft.Gpu;

//ILayout 布局容器接口对标原版 Layout extends LayoutElement
//管理子元素 VisitChildren/RemoveChildren/ArrangeElements
//ArrangeElements 默认递归子 Layout 先排子再排自己子类 override 加自身布局逻辑
public interface ILayout : ILayoutElement
{
    void VisitChildren(Action<ILayoutElement> visitor);

    void RemoveChildren();

    //ArrangeElements 默认递归子 Layout 子类 override 计算具体位置后调 base
    void ArrangeElements()
    {
        VisitChildren(child =>
        {
            if (child is ILayout layout)
                layout.ArrangeElements();
        });
    }
}
