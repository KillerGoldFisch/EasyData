import { expect } from "@olton/latte"

import { 
    DataColumnList, DataRow, DataType, EasyDataTable, 
    MetaData, MetaEntity, MetaEntityAttr, i18n 
} from '@easydata/core';

import { 
    DefaultDialogService, DialogService, 
    EasyGrid, GridCellRenderer, GridColumn 
} from '@easydata/ui';

import { DataContext } from '../src/main/data_context';
import { EntityDataView } from '../src/views/entity_data_view';
import { TextFilterWidget } from '../src/widgets/text_filter_widget';
import * as utils from '../src/utils/utils';

describe('EntityDataView', () => {
    // Mocks for DOM and objects
    let mockSlot: HTMLElement;
    let mockContext: DataContext;
    let mockDialogService: DialogService;
    let mockGrid: EasyGrid;
    let mockEntity: MetaEntity;
    let mockMetaData: MetaData;
    let mockDataTable: EasyDataTable;
    let mockFilterWidget: TextFilterWidget;
    let view: EntityDataView;

    // Helper function to create mock attribute
    function createMockAttr(id: string, options: any = {}): MetaEntityAttr {
        return {
            id,
            caption: options.caption || id,
            dataType: options.dataType || DataType.String,
            isPrimaryKey: options.isPrimaryKey || false,
            showOnView: options.showOnView !== undefined ? options.showOnView : true
        } as MetaEntityAttr;
    }

    beforeEach(() => {
        // Mock for HTMLElement
        mockSlot = document.createElement('div');
        document.body.appendChild(mockSlot);

        // Mock for entity attributes
        const mockAttrs = [
            createMockAttr('Entity.id', { caption: 'ID', dataType: DataType.Int32, isPrimaryKey: true }),
            createMockAttr('Entity.name', { caption: 'Name', dataType: DataType.String }),
            createMockAttr('Entity.description', { caption: 'Description', dataType: DataType.String }),
            createMockAttr('Entity.active', { caption: 'Active', dataType: DataType.Bool })
        ];

        // Mock for entities
        mockEntity = {
            id: 'Entity',
            name: 'Entity',
            caption: 'Entity',
            captionPlural: 'Entities',
            attributes: mockAttrs,
            isEditable: true,
            getPrimaryAttrs: mock().mockReturnValue([mockAttrs[0]])
        } as unknown as MetaEntity;

        // Mock for metadata
        mockMetaData = {
            getAttributeById: (id: string) => mockAttrs.find(attr => attr.id === id) || null
        } as unknown as MetaData;

        // Mock for data columns
        const mockColumns = new DataColumnList();
        mockColumns.add({ id: 'Entity.id', label: 'ID', type: DataType.Int32 });
        mockColumns.add({ id: 'Entity.name', label: 'Name', type: DataType.String });
        mockColumns.add({ id: 'Entity.description', label: 'Description', type: DataType.String });
        mockColumns.add({ id: 'Entity.active', label: 'Active', type: DataType.Bool });

        // Mock for data table
        mockDataTable = {
            columns: mockColumns,
            getRow: mock().mockImplementation((rowIndex: number) => {
                if (rowIndex === 0) {
                    return Promise.resolve({
                        getValue: (id: string) => {
                            if (id === 'Entity.id') return 1;
                            if (id === 'Entity.name') return 'Test Name';
                            if (id === 'Entity.description') return 'Test Description';
                            if (id === 'Entity.active') return true;
                            return null;
                        }
                    } as DataRow);
                }
                return Promise.resolve(null);
            }),
            getCachedRows: mock().mockReturnValue([])
        } as unknown as EasyDataTable;

        // Mock for data context
        mockContext = {
            getMetaData: mock().mockReturnValue(mockMetaData),
            getActiveEntity: mock().mockReturnValue(mockEntity),
            fetchDataset: mock().mockResolvedValue(mockDataTable),
            createFilter: mock(),
            createRecord: mock().mockResolvedValue({}),
            updateRecord: mock().mockResolvedValue({}),
            deleteRecord: mock().mockResolvedValue({})
        } as unknown as DataContext;

        // Mock for dialog service
        mockDialogService = {
            open: mock().mockReturnValue({
                submit: mock()
            }),
            openConfirm: mock().mockResolvedValue(true)
        } as unknown as DialogService;
        // jest.spyOn(DefaultDialogService.prototype, 'constructor').mockImplementation(() => {});
        // jest.spyOn(DefaultDialogService.prototype, 'open').mockImplementation(
        //     (options) => mockDialogService.open(options)
        // );
        // jest.spyOn(DefaultDialogService.prototype, 'openConfirm').mockImplementation(
        //     (title, message) => mockDialogService.openConfirm(title, message)
        // );

        // Mock for EasyGrid
        mockGrid = {
            refresh: mock(),
            getData: mock().mockReturnValue(mockDataTable)
        } as unknown as EasyGrid;
        // jest.spyOn(EasyGrid.prototype, 'constructor').mockImplementation(() => {});
        Object.defineProperty(EasyGrid, 'prototype', {
            value: mockGrid,
            writable: true
        });

        // Mock for TextFilterWidget
        mockFilterWidget = {
            applyFilter: mock().mockReturnValue(true)
        } as unknown as TextFilterWidget;
        // jest.spyOn(TextFilterWidget.prototype, 'constructor').mockImplementation(() => {});
        // jest.spyOn(TextFilterWidget.prototype, 'applyFilter').mockImplementation(
        //     (refresh) => mockFilterWidget.applyFilter(refresh)
        // );

        // Mock for i18n
        // jest.spyOn(i18n, 'getText').mockImplementation((key: string) => {
        //     if (key === 'BackToEntities') return 'Back to Entities';
        //     if (key === 'AddRecordBtnTitle') return 'Add Record';
        //     if (key === 'EditBtn') return 'Edit';
        //     if (key === 'DeleteBtn') return 'Delete';
        //     if (key === 'AddDlgCaption') return 'Add {entity}';
        //     if (key === 'EditDlgCaption') return 'Edit {entity}';
        //     if (key === 'DeleteDlgCaption') return 'Delete {entity}';
        //     if (key === 'DeleteDlgMessage') return 'Delete record with ID: {recordId}';
        //     return key;
        // });
        //
        // // Mock for utils.setLocation
        // jest.spyOn(utils, 'setLocation').mockImplementation(() => {});
    });

    afterEach(() => {
        // Remove added elements
        if (mockSlot.parentNode) {
            mockSlot.parentNode.removeChild(mockSlot);
        }

        // Reset mocks
        // jest.restoreAllMocks();
    });

    it('should be created with correct default settings', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Check that context is set
        expect((view as any).context).toBe(mockContext);
        
        // Check that base path is set
        expect((view as any).basePath).toBe('/basePath');
        
        // Check default options
        const options = (view as any).options;
        expect(options).toBeDefined();
        expect(options.showFilterBox).toBe(true);
        expect(options.showBackToEntities).toBe(true);
    });

    it('should render title and back button', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Check that title contains entity name
        expect(mockSlot.innerHTML).toContain('<h1>Entities</h1>');
        
        // Check presence of back button
        const backLink = mockSlot.querySelector('a');
        expect(backLink).toBeDefined();
        expect(backLink.textContent).toBe('← Back to Entities');
        
        // Check that the click handler is set
        const clickEvent = new MouseEvent('click');
        const preventDefaultSpy = jest.spyOn(clickEvent, 'preventDefault');
        backLink.dispatchEvent(clickEvent);
        
        expect(preventDefaultSpy).toHaveBeenCalled();
        expect(utils.setLocation).toHaveBeenCalledWith('/basePath');
    });

    it('should not render back button if showBackToEntities=false', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', { showBackToEntities: false });
        
        // Check absence of the back button
        const backLink = mockSlot.querySelector('a');
        expect(backLink).toBeNull();
    });

    it('should call fetchDataset and create grid', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Check that fetchDataset was called
        expect(mockContext.fetchDataset).toHaveBeenCalled();
    });

    it('should create filter if showFilterBox=true', () => {
        // Replace setTimeout to wait for async operations
        jest.useFakeTimers();
        
        view = new EntityDataView(mockSlot, mockContext, '/basePath', { showFilterBox: true });
        
        jest.runAllTimers();
        
        // Check filter creation
        expect(mockContext.createFilter).toHaveBeenCalled();
    });

    it('should not create filter if showFilterBox=false', () => {
        jest.useFakeTimers();
        
        view = new EntityDataView(mockSlot, mockContext, '/basePath', { showFilterBox: false });
        
        jest.runAllTimers();
        
        // Check that filter is not created
        expect(mockContext.createFilter).not.toHaveBeenCalled();
    });

    it('should correctly handle add button click', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Directly call the add button click handler
        (view as any).addClickHandler();
        
        // Check dialog invocation
        expect(mockDialogService.open).toHaveBeenCalled();
        const openArgs = (mockDialogService.open as jest.Mock).mock.calls[0][0];
        expect(openArgs).toBeObject();
        expect(openArgs.title).toBe('Add Entity');
    });

    it('should correctly handle edit button click', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Directly call the edit button click handler
        (view as any).editClickHandler(new MouseEvent('click'), 0);
        
        // Check getRow invocation
        expect(mockDataTable.getRow).toHaveBeenCalledWith(0);
        
        // Check that the edit dialog opens
        return mockDataTable.getRow(0).then(() => {
            expect(mockDialogService.open).toHaveBeenCalled();
            const openArgs = (mockDialogService.open as jest.Mock).mock.calls[0][0];
            expect(openArgs).toBeObject();
            expect(openArgs.title).toBe('Edit Entity');
        });
    });

    it('should correctly handle delete button click', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Directly call the delete button click handler
        (view as any).deleteClickHandler(new MouseEvent('click'), 0);
        
        // Check getRow invocation
        expect(mockDataTable.getRow).toHaveBeenCalledWith(0);
        
        // Check confirmation dialog opening
        return mockDataTable.getRow(0).then(() => {
            expect(mockDialogService.openConfirm).toHaveBeenCalled();
            
            // Check deleteRecord call after confirmation
            return mockDialogService.openConfirm("", "").then(() => {
                expect(mockContext.deleteRecord).toHaveBeenCalledWith({ id: 1 });
            });
        });
    });

    it('should update data after CRUD operations', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Replace filterWidget
        (view as any).filterWidget = mockFilterWidget;
        
        // Directly call refreshData
        return (view as any).refreshData().then(() => {
            // Check fetchDataset invocation
            expect(mockContext.fetchDataset).toHaveBeenCalled();
            
            // Check filter application
            expect(mockFilterWidget.applyFilter).toHaveBeenCalledWith(false);
        });
    });

    it('should update grid if filter is not applied', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Replace filterWidget with flag that filter was not applied
        (view as any).filterWidget = {
            applyFilter: mock().mockReturnValue(false)
        };
        
        // Directly call refreshData
        return (view as any).refreshData().then(() => {
            // Check refresh call on grid
            expect(mockGrid.refresh).toHaveBeenCalled();
        });
    });

    it('should correctly handle errors', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Create an error
        const error = new Error('Test error');
        
        // Directly call the error handler
        (view as any).processError(error);
        
        // Check error dialog opening
        expect(mockDialogService.open).toHaveBeenCalled();
        const openArgs = (mockDialogService.open as jest.Mock).mock.calls[0][0];
        expect(openArgs).toBeObject();
        expect(openArgs.title).toBe('Ooops, something went wrong');
        expect(openArgs.body).toBe('Test error');
    });

    it('should correctly manage cell renderer', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Create column with row number
        const column: GridColumn = {
            isRowNum: true
        } as GridColumn;
        
        // Create mock for defaultRenderer
        const defaultRenderer = mock() as GridCellRenderer;
        
        // Call the manageCellRenderer method
        const renderer = (view as any).manageCellRenderer(column, defaultRenderer);
        
        // Check that renderer function is returned
        expect(typeof renderer).toBe('function');
        
        // Check that column width is set
        expect(column.width).toBe(110);
        
        // Prepare elements for renderer testing
        const cell = document.createElement('div');
        const rowEl = document.createElement('tr');
        rowEl.setAttribute('data-row-idx', '0');
        
        // Call the renderer
        renderer('value', column, cell, rowEl);
        
        // Check that Edit and Delete buttons appear in the cell
        expect(cell.innerHTML).toContain('Edit');
        expect(cell.innerHTML).toContain('Delete');
    });

    it('should synchronize grid column visibility with metadata', () => {
        view = new EntityDataView(mockSlot, mockContext, '/basePath', {});
        
        // Create column with dataColumn
        const column: GridColumn = {
            dataColumn: {
                id: 'Entity.name'
            }
        } as GridColumn;
        
        // Set showOnView to false for the attribute
        const attr = mockMetaData.getAttributeById('Entity.name');
        attr.showOnView = false;
        
        // Call the syncGridColumnHandler method
        (view as any).syncGridColumnHandler(column);
        
        // Check that column visibility is set according to the showOnView attribute
        expect(column.isVisible).toBe(false);
    });

    it('should throw error if active entity is not found', () => {
        // Replace getActiveEntity to return null
        (mockContext.getActiveEntity as jest.Mock).mockReturnValue(null);
        
        // Check that the constructor throws an error
        expect(() => {
            new EntityDataView(mockSlot, mockContext, '/basePath', {});
        }).toThrow("Can't find active entity for " + window.location.pathname);
    });
});
