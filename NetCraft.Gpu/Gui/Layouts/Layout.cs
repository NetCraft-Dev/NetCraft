namespace NetCraft.Gpu;

//ILayout layout container interface, maps to vanilla Layout extends LayoutElement
//Manages children via VisitChildren/RemoveChildren/ArrangeElements
//ArrangeElements recursively arranges child Layouts by default, children before itself; subclasses override to add their logic
public interface ILayout : ILayoutElement
{
    void VisitChildren(Action<ILayoutElement> visitor);

    void RemoveChildren();

    //ArrangeElements recursively arranges child Layouts by default; subclasses override, compute positions and call base
    void ArrangeElements()
    {
        VisitChildren(child =>
        {
            if (child is ILayout layout)
                layout.ArrangeElements();
        });
    }
}
